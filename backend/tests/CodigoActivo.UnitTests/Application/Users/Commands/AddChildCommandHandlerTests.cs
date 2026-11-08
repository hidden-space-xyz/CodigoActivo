using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Accounts.Commands;
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
    private readonly TestCurrentUser currentUser = new();
    private readonly TestClock clock = new(today: Today);
    private readonly AddChildCommandHandler sut;

    public AddChildCommandHandlerTests()
    {
        sut = new AddChildCommandHandler(users, ActingUsers.Policy(currentUser), clock);
    }

    private Task<Result<UserId>> AddAsync(Guid parentId)
    {
        currentUser.Id = UserId.From(parentId);
        return sut.HandleAsync(
            new AddChildCommand(
                UserId.From(parentId),
                new MinorDraft("Kid", "Doe", MinorDob, Gender.Male, true)
            ),
            TestContext.Current.CancellationToken
        );
    }

    private Task AssertNotAddedAsync()
    {
        return users
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, TestContext.Current.CancellationToken);
    }

    private async Task<List<User>> CaptureAddedUsersAsync()
    {
        var added = new List<User>();
        await users.AddAsync(Arg.Do<User>(added.Add), Arg.Any<CancellationToken>());
        return added;
    }

    [Fact]
    public async Task HandleAsyncAdministratorAddingToAnotherHouseholdIsForbidden()
    {
        currentUser.IsAdmin = true;
        var guardian = NewUser();
        users.FindReturns(guardian);

        var result = await sut.HandleAsync(
            new AddChildCommand(
                guardian.Id,
                new MinorDraft("Kid", "Doe", MinorDob, Gender.Male, true)
            ),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Forbidden, ApplicationErrorCode.ActingForAnotherUserForbidden);
        await AssertNotAddedAsync();
    }

    [Fact]
    public async Task HandleAsyncParentMissingReturnsNotFound()
    {
        users.FindReturns(null);
        var request = new MinorDraft("Kid", "Doe", MinorDob, Gender.Male, true);
        var parentId = UserId.New();
        currentUser.Id = parentId;

        var result = await sut.HandleAsync(
            new AddChildCommand(parentId, request),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.ParentUserNotFound);
        await AssertNotAddedAsync();
    }

    [Fact]
    public async Task HandleAsyncWithoutTheGuardianshipDeclarationIsRefused()
    {
        users.FindReturns(NewUser());
        var request = new MinorDraft("Kid", "Doe", MinorDob, Gender.Male, false);
        var parentId = UserId.New();
        currentUser.Id = parentId;

        var result = await sut.HandleAsync(
            new AddChildCommand(parentId, request),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(
            ErrorKind.Validation,
            DomainErrorCode.UserGuardianshipDeclarationRequired
        );
        await AssertNotAddedAsync();
    }

    [Fact]
    public async Task HandleAsyncChildBirthDateNotMinorReturnsBadRequest()
    {
        users.FindReturns(NewUser());
        var request = new MinorDraft("Grown", "Up", AdultDob, Gender.Male, true);
        var parentId = UserId.New();
        currentUser.Id = parentId;

        var result = await sut.HandleAsync(
            new AddChildCommand(parentId, request),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, DomainErrorCode.UserChildBirthDateNotMinor);
        await AssertNotAddedAsync();
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
        await AssertNotAddedAsync();
    }

    [Fact]
    public async Task HandleAsyncValidRequestCreatesAndPersistsDependentChild()
    {
        var parentId = Guid.NewGuid();
        var parent = NewUser(id: parentId);
        users.FindReturns(parent);
        var added = await CaptureAddedUsersAsync();
        clock.UtcNow = new DateTimeOffset(2026, 3, 3, 0, 0, 0, TimeSpan.Zero);
        var request = new MinorDraft("  Kid  ", "  Doe  ", MinorDob, Gender.Female, true);
        currentUser.Id = UserId.From(parentId);

        var result = await sut.HandleAsync(
            new AddChildCommand(UserId.From(parentId), request),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var child = added.Should().ContainSingle().Which;
        result.Value.Should().Be(child.Id);
        child.BirthDate.Should().Be(MinorDob);
        child.NationalId.Should().BeNull();
        child.NewsConsent.Should().BeFalse();
        child.GuardianshipDeclaredAt.Should().Be(clock.UtcNow);
        await users
            .Received(1)
            .AddAsync(
                Arg.Is<User>(u => IsAddedChild(u, parentId, clock.UtcNow)),
                Arg.Any<CancellationToken>()
            );
    }
}
