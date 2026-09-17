using System.Security.Cryptography;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Security;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Auth;

/// <summary>
/// Matches authenticator codes against a protected shared secret, refusing replayed codes.
/// </summary>
/// <param name="totp">Time-based one-time password implementation.</param>
/// <param name="protector">Protector that decrypts stored authenticator keys.</param>
/// <param name="clock">Clock used to obtain the current time step.</param>
/// <param name="logger">Logger used to record unreadable keys.</param>
public sealed class AuthenticatorCodeVerifier(
    ITotpService totp,
    ISecretProtector protector,
    IClock clock,
    ILogger<AuthenticatorCodeVerifier> logger
)
{
    /// <summary>
    /// Finds the time step that produced the code, unless it was already used.
    /// </summary>
    /// <param name="protectedKey">Protected shared secret; <see langword="null"/> never matches.</param>
    /// <param name="code">Code typed by the user.</param>
    /// <param name="lastUsedStep">Most recent time step already accepted for this key.</param>
    /// <returns>The accepted time step, or <see langword="null"/> when the code is rejected.</returns>
    public long? Match(string? protectedKey, string code, long? lastUsedStep)
    {
        if (protectedKey is null)
        {
            return null;
        }

        string secret;
        try
        {
            secret = protector.Unprotect(protectedKey);
        }
        catch (CryptographicException ex)
        {
            logger.LogError(
                ex,
                "A stored authenticator key could not be decrypted; the data protection keys may have changed"
            );
            return null;
        }

        var step = totp.MatchStep(secret, code, clock.UtcNow);
        return step is not null && step <= lastUsedStep ? null : step;
    }
}
