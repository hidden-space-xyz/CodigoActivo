using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Commands;

public sealed class AddChildCommandHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly TestCurrentUser currentUser = new(isAdmin: true);
    private readonly TestClock clock = new(today: Today);
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly AddChildCommandHandler sut;

    public AddChildCommandHandlerTests()
    {
        sut = new AddChildCommandHandler(
            users,
            ActingUsers.Policy(currentUser),
            clock,
            uow.RunsTransactions()
        );
    }

    private Task<Result<UserId>> AddAsync(Guid parentId)
    {
        return sut.HandleAsync(
            new AddChildCommand(
                UserId.From(parentId),
                new MinorDraft("Kid", "Doe", MinorDob, Gender.Male)
            ),
            TestContext.Current.CancellationToken
        );
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<User>> CaptureAddedUsersAsync()
    {
        var added = new List<User>();
        await users.AddAsync(Arg.Do<User>(added.Add), Arg.Any<CancellationToken>());
        return added;
    }

    [Fact]
    public async Task HandleAsyncParentMissingReturnsNotFound()
    {
        users.FindReturns(null);
        var request = new MinorDraft("Kid", "Doe", MinorDob, Gender.Male);

        var result = await sut.HandleAsync(
            new AddChildCommand(UserId.New(), request),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.ParentUserNotFound);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncChildBirthDateNotMinorReturnsBadRequest()
    {
        users.FindReturns(NewUser());
        var request = new MinorDraft("Grown", "Up", AdultDob, Gender.Male);

        var result = await sut.HandleAsync(
            new AddChildCommand(UserId.New(), request),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, DomainErrorCode.UserChildBirthDateNotMinor);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncGuardianAtTheDependentLimitReturnsConflict()
    {
        var parent = NewUser();
        users.FindReturns(parent);
        users
            .CountDependentsAsync(parent.Id, Arg.Any<CancellationToken>())
            .Returns(Household.MaxDependents);

        var result = await AddAsync(parent.Id.Value);

        result.ShouldFail(ErrorKind.Conflict, DomainErrorCode.UserChildLimitReached);
        await users
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, TestContext.Current.CancellationToken);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncCountsTheDependentsWithTheGuardianRowLocked()
    {
        var parent = NewUser();
        users.FindReturns(parent);
        users
            .CountDependentsAsync(parent.Id, Arg.Any<CancellationToken>())
            .Returns(Household.MaxDependents - 1);

        var result = await AddAsync(parent.Id.Value);

        result.IsSuccess.Should().BeTrue();
        Received.InOrder(() =>
        {
            _ = uow.ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<Result>>>(),
                Arg.Any<CancellationToken>()
            );
            _ = users.LockAsync(parent, Arg.Any<CancellationToken>());
            _ = users.CountDependentsAsync(parent.Id, Arg.Any<CancellationToken>());
            _ = users.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
            _ = uow.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task HandleAsyncGuardianGoneOnceLockedReturnsNotFound()
    {
        var parent = NewUser();
        users.FindReturns(parent);
        users.LockAsync(parent, Arg.Any<CancellationToken>()).Returns(false);

        var result = await AddAsync(parent.Id.Value);

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.ParentUserNotFound);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncValidRequestCreatesDependentChildPersistsAndInvalidatesCache()
    {
        var parentId = Guid.NewGuid();
        var parent = NewUser(id: parentId);
        users.FindReturns(parent);
        var added = await CaptureAddedUsersAsync();
        clock.UtcNow = new DateTimeOffset(2026, 3, 3, 0, 0, 0, TimeSpan.Zero);
        var request = new MinorDraft("  Kid  ", "  Doe  ", MinorDob, Gender.Female);

        var result = await sut.HandleAsync(
            new AddChildCommand(UserId.From(parentId), request),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var child = added.Should().ContainSingle().Which;
        result.Value.Should().Be(child.Id);
        child.BirthDate.Should().Be(MinorDob);
        child.NationalId.Should().BeNull();
        child.PromotionalConsent.Should().BeFalse();
        await users
            .Received(1)
            .AddAsync(
                Arg.Is<User>(u => IsAddedChild(u, parentId, clock.UtcNow)),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
