using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Users;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
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
    private readonly IAccountErasureStore erasureStore = Substitute.For<IAccountErasureStore>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly DeleteUserCommandHandler sut;

    public DeleteUserCommandHandlerTests()
    {
        sut = new DeleteUserCommandHandler(
            users,
            AccountErasers.Create(users, deletedAccounts, erasureStore, uow),
            clock,
            cacheInvalidator
        );
    }

    private Task<Result> DeleteAsync(Guid userId, Guid actingUserId)
    {
        return sut.HandleAsync(
            new DeleteUserCommand(userId, actingUserId),
            TestContext.Current.CancellationToken
        );
    }

    private async Task AssertNotErasedAsync()
    {
        await erasureStore
            .DidNotReceiveWithAnyArgs()
            .CaptureLegalCopyAsync(default, default!, TestContext.Current.CancellationToken);
        await deletedAccounts
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, TestContext.Current.CancellationToken);
        users.DidNotReceiveWithAnyArgs().Remove(default!);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private ValueTask AssertCacheKeptAsync()
    {
        return cacheInvalidator
            .DidNotReceive()
            .InvalidateAsync(Arg.Any<IReadOnlyCollection<string>>());
    }

    private async Task AssertErasedAsync(User user, AccountDeletionOrigin origin, Guid actorId)
    {
        await erasureStore
            .Received(1)
            .CaptureLegalCopyAsync(
                user.Id,
                Arg.Is<AccountErasure>(erasure =>
                    erasure.Origin == origin
                    && erasure.ActorId == actorId
                    && erasure.DeletedAt == clock.UtcNow
                ),
                Arg.Any<CancellationToken>()
            );
        await deletedAccounts
            .Received(1)
            .AddAsync(
                Arg.Is<DeletedAccount>(copy =>
                    copy.Id == user.Id
                    && copy.DeletedAt == clock.UtcNow
                    && copy.Data == AccountErasers.LegalCopy
                ),
                Arg.Any<CancellationToken>()
            );
        users.Received(1).Remove(user);
    }

    [Fact]
    public async Task HandleAsyncTargetIsTheInitialAdministratorReturnsForbiddenWithoutLoadingIt()
    {
        var result = await DeleteAsync(SeedIds.Users.InitialAdministrator, Guid.NewGuid());

        result.ShouldFail(ErrorKind.Forbidden, ErrorCode.UserDeleteInitialAdminForbidden);
        await users
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default, TestContext.Current.CancellationToken);
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
        erasureStore.LockHouseholdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(false);

        var result = await DeleteAsync(user.Id, Guid.NewGuid());

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
        await AssertNotErasedAsync();
        await AssertCacheKeptAsync();
    }
}
