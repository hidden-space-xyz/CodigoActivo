using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Communication.Templates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.Extensions;

namespace CodigoActivo.UnitTests.TestSupport;

public static class PasswordGuards
{
    public static PasswordAttemptGuard Create(
        IPasswordHasher hasher,
        IUnitOfWork uow,
        IClock clock,
        IUserSessionRepository? sessions = null,
        IEmailSender? emailSender = null,
        PasswordLockoutOptions? options = null,
        ILogger<PasswordAttemptGuard>? logger = null,
        IUserRepository? users = null
    )
    {
        return new PasswordAttemptGuard(
            LockingPasswordState(users ?? Substitute.For<IUserRepository>()),
            sessions ?? Substitute.For<IUserSessionRepository>(),
            uow.RunsTransactions(),
            clock,
            hasher,
            new CredentialTimingProtector(hasher),
            options ?? new PasswordLockoutOptions(),
            new AccountSecurityNotifier(
                emailSender ?? new RecordingEmailSender(),
                clock,
                new AccountEmailComposer(new ApplicationOptions(), clock),
                NullLogger<AccountSecurityNotifier>.Instance
            ),
            logger ?? NullLogger<PasswordAttemptGuard>.Instance
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
