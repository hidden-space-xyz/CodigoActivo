using CodigoActivo.Application.Auth;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
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
        ILogger<PasswordAttemptGuard>? logger = null
    )
    {
        return new PasswordAttemptGuard(
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
}
