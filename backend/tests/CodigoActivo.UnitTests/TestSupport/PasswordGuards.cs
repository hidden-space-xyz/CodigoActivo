using CodigoActivo.Application.Auth;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

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
            CountingFailures(users ?? Substitute.For<IUserRepository>()),
            sessions ?? Substitute.For<IUserSessionRepository>(),
            uow,
            clock,
            hasher,
            new CredentialTimingProtector(hasher),
            options ?? new PasswordLockoutOptions(),
            new AccountSecurityNotifier(
                emailSender ?? new RecordingEmailSender(),
                clock,
                new ApplicationOptions(),
                NullLogger<AccountSecurityNotifier>.Instance
            ),
            logger ?? NullLogger<PasswordAttemptGuard>.Instance
        );
    }

    /// <summary>
    /// Mirrors in memory what <see cref="IUserRepository.RecordPasswordFailureAsync"/> writes with a
    /// single statement: one more failure, a lock once the limit is reached and no pending challenge
    /// left, reported only to the caller that locked the account.
    /// </summary>
    private static IUserRepository CountingFailures(IUserRepository users)
    {
        users
            .RecordPasswordFailureAsync(
                Arg.Any<User>(),
                Arg.Any<int>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(call =>
            {
                var user = call.Arg<User>();
                if (user.IsPasswordLocked())
                {
                    return false;
                }

                user.PasswordFailedAttempts++;
                if (user.PasswordFailedAttempts < call.ArgAt<int>(1))
                {
                    return false;
                }

                user.PasswordLockedAt = call.ArgAt<DateTimeOffset>(2);
                user.ClearLoginChallenge();
                return true;
            });
        return users;
    }
}
