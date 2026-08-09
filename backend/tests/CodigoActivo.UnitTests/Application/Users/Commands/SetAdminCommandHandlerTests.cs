using System.Linq.Expressions;
using AwesomeAssertions;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Commands;

public sealed class SetAdminCommandHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly TestClock clock = new(today: Today);
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly SetAdminCommandHandler sut;

    public SetAdminCommandHandlerTests()
    {
        sut = new SetAdminCommandHandler(users, clock, uow);
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.FindReturns(null);

        var result = await sut.HandleAsync(
            new SetAdminCommand(Guid.NewGuid(), true),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncGrantAdminToNonAdminGrantsAndSaves()
    {
        var user = NewUser(isAdmin: false);
        users.FindReturns(user);

        var result = await sut.HandleAsync(
            new SetAdminCommand(user.Id, true),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.IsAdmin.Should().BeTrue();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncFlagUnchangedIsNoopAndDoesNotSave()
    {
        var user = NewUser(isAdmin: true);
        users.FindReturns(user);

        var result = await sut.HandleAsync(
            new SetAdminCommand(user.Id, true),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncRevokeWithOtherAdminsRemainingRevokesAndSaves()
    {
        var user = NewUser(isAdmin: true);
        users.FindReturns(user);
        users
            .CountAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(2);

        var result = await sut.HandleAsync(
            new SetAdminCommand(user.Id, false),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.IsAdmin.Should().BeFalse();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncRevokeLastAdminReturnsForbidden()
    {
        var user = NewUser(isAdmin: true);
        users.FindReturns(user);
        users
            .CountAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(1);

        var result = await sut.HandleAsync(
            new SetAdminCommand(user.Id, false),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Forbidden, ErrorCode.UserCannotRemoveLastAdmin);
        user.IsAdmin.Should().BeTrue();
        await AssertNotSavedAsync();
    }
}
