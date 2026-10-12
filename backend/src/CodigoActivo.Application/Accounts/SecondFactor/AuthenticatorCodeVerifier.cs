using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;

namespace CodigoActivo.Application.Accounts.SecondFactor;

/// <summary>
/// Matches authenticator codes against a protected shared secret. Whether the time step of a code
/// may still be accepted is decided by the account.
/// </summary>
/// <param name="totp">Time-based one-time password implementation.</param>
/// <param name="protector">Protector that decrypts stored authenticator keys.</param>
/// <param name="clock">Clock used to obtain the current time step.</param>
public sealed class AuthenticatorCodeVerifier(
    ITotpService totp,
    ISecretProtector protector,
    IClock clock
)
{
    /// <summary>
    /// Finds the time step that produced the code.
    /// </summary>
    /// <param name="protectedKey">Protected shared secret; <see langword="null"/> never matches.</param>
    /// <param name="code">Code typed by the user.</param>
    /// <returns>The matching time step, or <see langword="null"/> when the code matches none.</returns>
    public long? Match(string? protectedKey, string code)
    {
        if (protectedKey is null)
        {
            return null;
        }

        return protector.TryUnprotect(protectedKey, out var secret)
            ? totp.MatchStep(secret, code, clock.UtcNow)
            : null;
    }
}
