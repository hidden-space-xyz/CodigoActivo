using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Commands;

public sealed class ChangeUserTypeCommandHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly TestClock clock = new(today: Today);
    private readonly TestCurrentUser currentUser = new(isAdmin: true);
    private readonly ChangeUserTypeCommandHandler sut;

    public ChangeUserTypeCommandHandlerTests()
    {
        sut = new ChangeUserTypeCommandHandler(users, currentUser, clock);
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.FindReturns(null);

        var result = await sut.HandleAsync(
            new ChangeUserTypeCommand(UserId.New(), UserType.Member),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleAsyncOwnAccountReturnsForbiddenAndKeepsTheType(bool isAdmin)
    {
        var user = NewUser(dob: AdultDob, isAdmin: isAdmin);
        currentUser.Id = user.Id;
        currentUser.IsAdmin = isAdmin;
        users.FindReturns(user);

        var result = await sut.HandleAsync(
            new ChangeUserTypeCommand(user.Id, UserType.Member),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Forbidden, ApplicationErrorCode.UserSelfTypeChangeForbidden);
        user.UserType.Should().Be(UserType.Participant);
        user.PullDomainEvents().Should().BeEmpty();
        await users
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncNewTypeDiffersFromCurrentReplacesTypeSaves()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, dob: AdultDob);
        users.FindReturns(user);
        clock.UtcNow = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);

        var result = await sut.HandleAsync(
            new ChangeUserTypeCommand(UserId.From(id), UserType.Member),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.UserType.Should().Be(UserType.Member);
        user.UpdatedAt.Should().Be(clock.UtcNow);
        user.PullDomainEvents().Should().Equal(new UserTypeChanged(user.Id));
    }

    [Fact]
    public async Task HandleAsyncTypeUnchangedChangesNothing()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, dob: AdultDob);
        Persisted.Overwrite(user, new { UserType = UserType.Member });
        users.FindReturns(user);

        var result = await sut.HandleAsync(
            new ChangeUserTypeCommand(UserId.From(id), UserType.Member),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.PullDomainEvents().Should().BeEmpty();
    }
}
