using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts.Erasure;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Accounts.Erasure;

public sealed class AccountEraserTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IAccountErasureStore erasureStore = Substitute.For<IAccountErasureStore>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly AccountEraser sut;

    public AccountEraserTests()
    {
        sut = AccountErasers.Create(users, erasureStore, uow);
    }

    [Fact]
    public async Task EraseAsyncHouseholdAlreadyGoneReturnsFalseAndStagesNothing()
    {
        var account = NewUser();
        erasureStore.LockHouseholdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(false);

        var erased = await sut.EraseAsync(account, TestContext.Current.CancellationToken);

        erased.Should().BeFalse();
        await erasureStore
            .DidNotReceiveWithAnyArgs()
            .LoadHouseholdAsync(default, TestContext.Current.CancellationToken);
        await erasureStore
            .DidNotReceiveWithAnyArgs()
            .HandOverAuthoredContentAsync(default, default, TestContext.Current.CancellationToken);
        users.DidNotReceiveWithAnyArgs().Remove(default!);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
        account.PullDomainEvents().Should().BeEmpty();
    }

    [Fact]
    public async Task EraseAsyncLockedHouseholdLoadsHandsOverRemovesAndSavesInOrder()
    {
        var account = NewUser();

        var erased = await sut.EraseAsync(account, TestContext.Current.CancellationToken);

        erased.Should().BeTrue();
        Received.InOrder(() =>
        {
            _ = uow.ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<bool>>>(),
                Arg.Any<CancellationToken>()
            );
            _ = erasureStore.LockHouseholdAsync(account.Id, Arg.Any<CancellationToken>());
            _ = erasureStore.LoadHouseholdAsync(account.Id, Arg.Any<CancellationToken>());
            _ = erasureStore.HandOverAuthoredContentAsync(
                account.Id,
                InitialAdministrator.Id,
                Arg.Any<CancellationToken>()
            );
            users.Remove(account);
            _ = uow.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
        account.PullDomainEvents().Should().ContainEquivalentOf(new AccountErased(account.Id));
    }

    [Fact]
    public async Task EraseAsyncRunsTheWholeErasureInASingleTransaction()
    {
        var account = NewUser();

        await sut.EraseAsync(account, TestContext.Current.CancellationToken);

        await uow.Received(1)
            .ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<bool>>>(),
                TestContext.Current.CancellationToken
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        users.Received(1).Remove(account);
    }

    [Fact]
    public async Task EraseAsyncMissingAccountThrowsBeforeTheTransaction()
    {
        var withoutAccount = () => sut.EraseAsync(null!, TestContext.Current.CancellationToken);

        await withoutAccount.Should().ThrowAsync<ArgumentNullException>();
        await uow.DidNotReceiveWithAnyArgs()
            .ExecuteInTransactionAsync<bool>(default!, TestContext.Current.CancellationToken);
    }
}
