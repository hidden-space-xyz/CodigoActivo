using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts.Erasure;
using CodigoActivo.Domain.Users;
using NSubstitute;

namespace CodigoActivo.UnitTests.TestSupport;

public static class AccountErasers
{
    public static AccountEraser Create(
        IUserRepository users,
        IAccountErasureStore erasureStore,
        IUnitOfWork uow
    )
    {
        erasureStore
            .LockHouseholdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(true);
        return new AccountEraser(users, erasureStore, uow.RunsTransactions());
    }
}
