using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Users;
using CodigoActivo.Domain.Users;
using NSubstitute;

namespace CodigoActivo.UnitTests.TestSupport;

public static class AccountErasers
{
    public const string LegalCopy = "{\"account\":{}}";

    public static AccountEraser Create(
        IUserRepository users,
        IDeletedAccountRepository deletedAccounts,
        IAccountErasureStore erasureStore,
        IUnitOfWork uow
    )
    {
        erasureStore
            .LockHouseholdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);
        erasureStore
            .CaptureLegalCopyAsync(
                Arg.Any<Guid>(),
                Arg.Any<AccountErasure>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(LegalCopy);
        return new AccountEraser(users, deletedAccounts, erasureStore, uow.RunsTransactions());
    }
}
