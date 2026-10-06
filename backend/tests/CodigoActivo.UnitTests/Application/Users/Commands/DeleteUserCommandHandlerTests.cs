using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
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
    private readonly TestCurrentUser currentUser = new(isAdmin: true);
    private readonly FakeReadStore guardianships = new();
    private readonly DeleteUserCommandHandler sut;

    public DeleteUserCommandHandlerTests()
    {
        sut = new DeleteUserCommandHandler(
            users,
            ActingUsers.Policy(currentUser, guardianships),
            currentUser,
            AccountErasers.Create(users, deletedAccounts, erasureStore, uow),
            clock
        );
    }

    private Task<Result> DeleteAsync(Guid userId, Guid actingUserId, bool isAdmin = true)
    {
        currentUser.Id = UserId.From(actingUserId);
        currentUser.IsAdmin = isAdmin;
        return sut.HandleAsync(
            new DeleteUserCommand(UserId.From(userId)),
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

    private async Task AssertErasedAsync(User user, AccountDeletionOrigin origin, Guid actorId)
    {
        await erasureStore
            .Received(1)
            .CaptureLegalCopyAsync(
                user.Id,
                Arg.Is<AccountErasure>(erasure =>
                    erasure.Origin == origin
                    && erasure.ActorId == UserId.From(actorId)
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
        var result = await DeleteAsync(KnownIds.Users.InitialAdministrator, Guid.NewGuid());

        result.ShouldFail(ErrorKind.Forbidden, DomainErrorCode.UserDeleteInitialAdminForbidden);
        await users
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default, TestContext.Current.CancellationToken);
        await AssertNotErasedAsync();
    }

    [Fact]
    public async Task HandleAsyncTargetIsAnotherAdministratorErasesItAsAdministrator()
    {
        var user = NewUser(isAdmin: true);
        users.FindReturns(user);
        var administratorId = Guid.NewGuid();

        var result = await DeleteAsync(user.Id.Value, administratorId);

        result.IsSuccess.Should().BeTrue();
        await AssertErasedAsync(user, AccountDeletionOrigin.Administrator, administratorId);
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.FindReturns(null);

        var result = await DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
        await AssertNotErasedAsync();
    }

    [Fact]
    public async Task HandleAsyncActingUserIsTheTargetReturnsForbidden()
    {
        var user = NewUser(isAdmin: false);
        users.FindReturns(user);

        var result = await DeleteAsync(user.Id.Value, user.Id.Value);

        result.ShouldFail(
            ErrorKind.Forbidden,
            ApplicationErrorCode.UserSelfDeleteRequiresVerification
        );
        await AssertNotErasedAsync();
    }

    [Fact]
    public async Task HandleAsyncAdministratorErasesAsAdministrator()
    {
        var user = NewUser(isAdmin: false);
        users.FindReturns(user);
        var administratorId = Guid.NewGuid();

        var result = await DeleteAsync(user.Id.Value, administratorId);

        result.IsSuccess.Should().BeTrue();
        await AssertErasedAsync(user, AccountDeletionOrigin.Administrator, administratorId);
    }

    [Fact]
    public async Task HandleAsyncGuardianErasesTheirMinorAsGuardian()
    {
        var guardianId = Guid.NewGuid();
        var minor = NewUser(first: "Leo", parentId: guardianId);
        users.FindReturns(minor);
        guardianships.AddDependent(minor.Id.Value, guardianId);

        var result = await DeleteAsync(minor.Id.Value, guardianId, isAdmin: false);

        result.IsSuccess.Should().BeTrue();
        await AssertErasedAsync(minor, AccountDeletionOrigin.Guardian, guardianId);
    }

    [Fact]
    public async Task HandleAsyncAdministratorErasingSomebodysMinorErasesAsAdministrator()
    {
        var minor = NewUser(first: "Leo", parentId: Guid.NewGuid());
        users.FindReturns(minor);
        var administratorId = Guid.NewGuid();

        var result = await DeleteAsync(minor.Id.Value, administratorId);

        result.IsSuccess.Should().BeTrue();
        await AssertErasedAsync(minor, AccountDeletionOrigin.Administrator, administratorId);
    }

    [Fact]
    public async Task HandleAsyncUserErasedConcurrentlyReturnsNotFound()
    {
        var user = NewUser(isAdmin: false);
        users.FindReturns(user);
        erasureStore.LockHouseholdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(false);

        var result = await DeleteAsync(user.Id.Value, Guid.NewGuid());

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
        await AssertNotErasedAsync();
    }

    [Fact]
    public async Task HandleAsyncSomeoneOutsideTheHouseholdReturnsForbidden()
    {
        var user = NewUser(isAdmin: false);
        users.FindReturns(user);

        var result = await DeleteAsync(user.Id.Value, Guid.NewGuid(), isAdmin: false);

        result.ShouldFail(ErrorKind.Forbidden, ApplicationErrorCode.ActingForAnotherUserForbidden);
        await AssertNotErasedAsync();
    }
}
