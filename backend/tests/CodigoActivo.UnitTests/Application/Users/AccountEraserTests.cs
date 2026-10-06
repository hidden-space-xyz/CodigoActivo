using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Users;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users;

public sealed class AccountEraserTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IDeletedAccountRepository deletedAccounts =
        Substitute.For<IDeletedAccountRepository>();
    private readonly IAccountErasureStore erasureStore = Substitute.For<IAccountErasureStore>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly AccountEraser sut;

    public AccountEraserTests()
    {
        sut = AccountErasers.Create(users, deletedAccounts, erasureStore, uow);
    }

    private static AccountErasure ErasureBy(Guid actorId)
    {
        return new AccountErasure(AccountDeletionOrigin.Administrator, UserId.From(actorId), Now);
    }

    [Fact]
    public async Task EraseAsyncHouseholdAlreadyGoneReturnsFalseAndStagesNothing()
    {
        var account = NewUser();
        erasureStore.LockHouseholdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(false);

        var erased = await sut.EraseAsync(
            account,
            ErasureBy(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        erased.Should().BeFalse();
        await erasureStore
            .DidNotReceiveWithAnyArgs()
            .CaptureLegalCopyAsync(default, default!, TestContext.Current.CancellationToken);
        await erasureStore
            .DidNotReceiveWithAnyArgs()
            .HandOverAuthoredContentAsync(default, default, TestContext.Current.CancellationToken);
        await deletedAccounts
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, TestContext.Current.CancellationToken);
        users.DidNotReceiveWithAnyArgs().Remove(default!);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EraseAsyncLockedHouseholdCopiesHandsOverArchivesRemovesAndSavesInOrder()
    {
        var account = NewUser();
        var erasure = ErasureBy(Guid.NewGuid());

        var erased = await sut.EraseAsync(account, erasure, TestContext.Current.CancellationToken);

        erased.Should().BeTrue();
        Received.InOrder(() =>
        {
            _ = uow.ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<bool>>>(),
                Arg.Any<CancellationToken>()
            );
            _ = erasureStore.LockHouseholdAsync(account.Id, Arg.Any<CancellationToken>());
            _ = erasureStore.CaptureLegalCopyAsync(
                account.Id,
                erasure,
                Arg.Any<CancellationToken>()
            );
            _ = erasureStore.HandOverAuthoredContentAsync(
                account.Id,
                InitialAdministrator.Id,
                Arg.Any<CancellationToken>()
            );
            _ = deletedAccounts.AddAsync(
                Arg.Is<DeletedAccount>(copy =>
                    copy.Id == account.Id
                    && copy.DeletedAt == Now
                    && copy.Data == AccountErasers.LegalCopy
                ),
                Arg.Any<CancellationToken>()
            );
            users.Remove(account);
            _ = uow.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task EraseAsyncRunsTheWholeErasureInASingleTransaction()
    {
        var account = NewUser();

        await sut.EraseAsync(
            account,
            ErasureBy(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        await uow.Received(1)
            .ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<bool>>>(),
                TestContext.Current.CancellationToken
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await deletedAccounts
            .Received(1)
            .AddAsync(Arg.Any<DeletedAccount>(), Arg.Any<CancellationToken>());
        users.Received(1).Remove(account);
    }

    [Fact]
    public async Task EraseAsyncMissingAccountOrErasureThrows()
    {
        var withoutAccount = () =>
            sut.EraseAsync(null!, ErasureBy(Guid.NewGuid()), TestContext.Current.CancellationToken);
        var withoutErasure = () =>
            sut.EraseAsync(NewUser(), null!, TestContext.Current.CancellationToken);

        await withoutAccount.Should().ThrowAsync<ArgumentNullException>();
        await withoutErasure.Should().ThrowAsync<ArgumentNullException>();
        await uow.DidNotReceiveWithAnyArgs()
            .ExecuteInTransactionAsync<bool>(default!, TestContext.Current.CancellationToken);
    }
}
