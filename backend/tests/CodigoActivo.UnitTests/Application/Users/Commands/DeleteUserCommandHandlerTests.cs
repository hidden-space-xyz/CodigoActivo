using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Commands;

public sealed class DeleteUserCommandHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly DeleteUserCommandHandler sut;

    public DeleteUserCommandHandlerTests()
    {
        sut = new DeleteUserCommandHandler(users, uow, cacheInvalidator);
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncTargetIsAdminReturnsForbidden()
    {
        var id = Guid.NewGuid();
        users.FindReturns(NewUser(id: id, isAdmin: true));

        var result = await sut.HandleAsync(
            new DeleteUserCommand(id),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Forbidden, ErrorCode.UserDeleteAdminForbidden);
        users.DidNotReceiveWithAnyArgs().Remove(Arg.Any<User>());
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.FindReturns(null);

        var result = await sut.HandleAsync(
            new DeleteUserCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
        await AssertNotSavedAsync();
        await cacheInvalidator
            .DidNotReceive()
            .InvalidateAsync(Arg.Any<IReadOnlyCollection<string>>());
    }

    [Fact]
    public async Task HandleAsyncTargetIsNonAdminRemovesSavesAndInvalidatesCache()
    {
        var user = NewUser(isAdmin: false);
        users.FindReturns(user);

        var result = await sut.HandleAsync(
            new DeleteUserCommand(user.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        users.Received(1).Remove(user);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null
                    && tags.Contains(CacheTags.Users)
                    && tags.Contains(CacheTags.Activities)
                )
            );
    }
}
