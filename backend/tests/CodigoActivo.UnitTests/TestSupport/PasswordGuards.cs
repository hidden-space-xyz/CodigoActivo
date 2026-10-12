using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Passwords;
using CodigoActivo.Domain.Users;
using NSubstitute;
using NSubstitute.Extensions;

namespace CodigoActivo.UnitTests.TestSupport;

public static class PasswordGuards
{
    public static PasswordAttemptGuard Create(
        IPasswordHasher hasher,
        IUnitOfWork uow,
        IClock clock,
        PasswordLockoutOptions? options = null,
        IUserRepository? users = null,
        ICurrentUser? currentUser = null
    )
    {
        return new PasswordAttemptGuard(
            LockingPasswordState(users ?? Substitute.For<IUserRepository>()),
            currentUser ?? new TestCurrentUser(),
            uow.RunsTransactions(),
            clock,
            hasher,
            new CredentialTimingProtector(hasher),
            options ?? new PasswordLockoutOptions()
        );
    }

    private static IUserRepository LockingPasswordState(IUserRepository users)
    {
        users
            .Configure()
            .LockPasswordStateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(true);
        return users;
    }
}
