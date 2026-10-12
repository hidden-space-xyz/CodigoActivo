using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.SecondFactor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodigoActivo.UnitTests.TestSupport;

public static class SecondFactorGuards
{
    public static SecondFactorGuard Create(
        IUnitOfWork uow,
        IClock clock,
        ITotpService totp,
        TwoFactorOptions options,
        ILogger<SecondFactorGuard>? logger = null
    )
    {
        return new SecondFactorGuard(
            uow,
            new OtpValidator(new FakeOneTimeCodeHasher()),
            new AuthenticatorCodeVerifier(totp, new FakeSecretProtector(), clock),
            options,
            logger ?? NullLogger<SecondFactorGuard>.Instance
        );
    }
}
