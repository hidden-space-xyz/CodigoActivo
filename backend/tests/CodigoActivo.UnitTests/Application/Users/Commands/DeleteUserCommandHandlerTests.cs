using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
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
    private readonly IDeletedAccountRepository deletedAccounts =
        Substitute.For<IDeletedAccountRepository>();
    private readonly TestClock clock = new();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly DeleteUserCommandHandler sut;

    public DeleteUserCommandHandlerTests()
    {
        sut = new DeleteUserCommandHandler(users, deletedAccounts, clock, cacheInvalidator);
        deletedAccounts
            .EraseAsync(Arg.Any<User>(), Arg.Any<AccountErasure>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    private Task<Result> DeleteAsync(Guid userId, Guid actingUserId)
    {
        return sut.HandleAsync(
            new DeleteUserCommand(userId, actingUserId),
            TestContext.Current.CancellationToken
        );
    }

    private Task<bool> AssertNotErasedAsync()
    {
        return deletedAccounts
            .DidNotReceiveWithAnyArgs()
            .EraseAsync(default!, default!, TestContext.Current.CancellationToken);
    }

    private ValueTask AssertCacheKeptAsync()
    {
        return cacheInvalidator
            .DidNotReceive()
            .InvalidateAsync(Arg.Any<IReadOnlyCollection<string>>());
    }

    private Task<bool> AssertErasedAsync(User user, AccountDeletionOrigin origin, Guid actorId)
    {
        return deletedAccounts
            .Received(1)
            .EraseAsync(
                user,
                new AccountErasure(origin, actorId, clock.UtcNow),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task HandleAsyncTargetIsTheInitialAdministratorReturnsForbiddenWithoutLoadingIt()
    {
        var result = await DeleteAsync(SeedIds.Users.InitialAdministrator, Guid.NewGuid());

        result.ShouldFail(ErrorKind.Forbidden, ErrorCode.UserDeleteInitialAdminForbidden);
        await users
            .DidNotReceiveWithAnyArgs()
            .FindAsync(default!, TestContext.Current.CancellationToken);
        await AssertNotErasedAsync();
        await AssertCacheKeptAsync();
    }

    [Fact]
    public async Task HandleAsyncTargetIsAnotherAdministratorErasesItAsAdministrator()
    {
        var user = NewUser(isAdmin: true);
        users.FindReturns(user);
        var administratorId = Guid.NewGuid();

        var result = await DeleteAsync(user.Id, administratorId);

        result.IsSuccess.Should().BeTrue();
        await AssertErasedAsync(user, AccountDeletionOrigin.Administrator, administratorId);
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.FindReturns(null);

        var result = await DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
        await AssertNotErasedAsync();
        await AssertCacheKeptAsync();
    }

    [Fact]
    public async Task HandleAsyncActingUserIsTheTargetReturnsForbidden()
    {
        var user = NewUser(isAdmin: false);
        users.FindReturns(user);

        var result = await DeleteAsync(user.Id, user.Id);

        result.ShouldFail(ErrorKind.Forbidden, ErrorCode.UserSelfDeleteRequiresVerification);
        await AssertNotErasedAsync();
        await AssertCacheKeptAsync();
    }

    [Fact]
    public async Task HandleAsyncAdministratorErasesAsAdministratorAndInvalidatesCache()
    {
        var user = NewUser(isAdmin: false);
        users.FindReturns(user);
        var administratorId = Guid.NewGuid();

        var result = await DeleteAsync(user.Id, administratorId);

        result.IsSuccess.Should().BeTrue();
        await AssertErasedAsync(user, AccountDeletionOrigin.Administrator, administratorId);
        users.DidNotReceiveWithAnyArgs().Remove(Arg.Any<User>());
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.SequenceEqual(CacheTags.Erasure)
                )
            );
    }

    [Fact]
    public async Task HandleAsyncGuardianErasesTheirMinorAsGuardian()
    {
        var guardianId = Guid.NewGuid();
        var minor = NewUser(first: "Leo", parentId: guardianId);
        users.FindReturns(minor);

        var result = await DeleteAsync(minor.Id, guardianId);

        result.IsSuccess.Should().BeTrue();
        await AssertErasedAsync(minor, AccountDeletionOrigin.Guardian, guardianId);
    }

    [Fact]
    public async Task HandleAsyncAdministratorErasingSomebodysMinorErasesAsAdministrator()
    {
        var minor = NewUser(first: "Leo", parentId: Guid.NewGuid());
        users.FindReturns(minor);
        var administratorId = Guid.NewGuid();

        var result = await DeleteAsync(minor.Id, administratorId);

        result.IsSuccess.Should().BeTrue();
        await AssertErasedAsync(minor, AccountDeletionOrigin.Administrator, administratorId);
    }

    [Fact]
    public async Task HandleAsyncUserErasedConcurrentlyReturnsNotFoundAndKeepsTheCache()
    {
        var user = NewUser(isAdmin: false);
        users.FindReturns(user);
        deletedAccounts
            .EraseAsync(user, Arg.Any<AccountErasure>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await DeleteAsync(user.Id, Guid.NewGuid());

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
        await AssertCacheKeptAsync();
    }
}
