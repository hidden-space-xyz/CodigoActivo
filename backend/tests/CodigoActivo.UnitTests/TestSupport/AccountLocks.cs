using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Domain.Users;
using NSubstitute;
using NSubstitute.Extensions;

namespace CodigoActivo.UnitTests.TestSupport;

public static class AccountLocks
{
    public static AccountLock Create(IUserRepository users, IUnitOfWork uow)
    {
        return new AccountLock(users.LocksEveryAccount(), uow.RunsTransactions());
    }

    public static IUserRepository LocksEveryAccount(this IUserRepository users)
    {
        users.Configure().LockAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns(true);
        return users;
    }

    public static void ReloadsAs(this IUserRepository users, User account, object stored)
    {
        users
            .LockAsync(account, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Persisted.Overwrite(account, stored);
                return true;
            });
    }
}
