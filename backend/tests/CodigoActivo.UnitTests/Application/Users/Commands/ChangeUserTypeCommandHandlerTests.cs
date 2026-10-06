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
    private readonly ChangeUserTypeCommandHandler sut;

    public ChangeUserTypeCommandHandlerTests()
    {
        sut = new ChangeUserTypeCommandHandler(users, clock);
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.FindReturns(null);

        var result = await sut.HandleAsync(
            new ChangeUserTypeCommand(UserId.New(), Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
    }

    [Fact]
    public async Task HandleAsyncRoleMissingReturnsNotFound()
    {
        var roleId = Guid.NewGuid();
        users.FindReturns(NewUser());

        var result = await sut.HandleAsync(
            new ChangeUserTypeCommand(UserId.New(), roleId),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserTypeNotFound);
    }

    [Fact]
    public async Task HandleAsyncNewTypeDiffersFromCurrentReplacesTypeSaves()
    {
        var id = Guid.NewGuid();
        var roleId = KnownIds.UserTypes.Member;
        var user = NewUser(id: id, dob: AdultDob);
        users.FindReturns(user);
        clock.UtcNow = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);

        var result = await sut.HandleAsync(
            new ChangeUserTypeCommand(UserId.From(id), roleId),
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
        var roleId = KnownIds.UserTypes.Member;
        var user = NewUser(id: id, dob: AdultDob);
        Persisted.Overwrite(user, new { UserType = UserType.Member });
        users.FindReturns(user);

        var result = await sut.HandleAsync(
            new ChangeUserTypeCommand(UserId.From(id), roleId),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.PullDomainEvents().Should().BeEmpty();
    }
}
