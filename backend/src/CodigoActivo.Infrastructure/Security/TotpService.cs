using CodigoActivo.Domain.Security;
using OtpNet;

namespace CodigoActivo.Infrastructure.Security;

/// <summary>
/// Time-based one-time passwords delegated to Otp.NET with the RFC 6238 profile every authenticator
/// application supports: HMAC-SHA1, six digits, thirty-second steps and one step of clock drift in
/// each direction. No cryptography is implemented here.
/// </summary>
public sealed class TotpService : ITotpService
{
    private const int SecretSize = 20;
    private const int Digits = 6;
    private const int StepSeconds = 30;
    private static readonly VerificationWindow Window = new(previous: 1, future: 1);

    /// <summary>
    /// Creates a new random shared secret encoded in Base32 without padding.
    /// </summary>
    /// <returns>The generated secret.</returns>
    public string GenerateSecret()
    {
        return Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(SecretSize)).TrimEnd('=');
    }

    /// <summary>
    /// Finds the time step, within the accepted clock drift, whose code equals the supplied one.
    /// </summary>
    /// <param name="secret">Base32 shared secret of the authenticator.</param>
    /// <param name="code">Code typed by the user.</param>
    /// <param name="now">Current timestamp used to compute the candidate time steps.</param>
    /// <returns>The matching time step, or <see langword="null"/> when no candidate matches.</returns>
    public long? MatchStep(string secret, string code, DateTimeOffset now)
    {
        if (!TryNormalizeCode(code, out var typed) || !TryDecodeSecret(secret, out var key))
        {
            return null;
        }

        return CreateTotp(key).VerifyTotp(now.UtcDateTime, typed, out var matchedStep, Window)
            ? matchedStep
            : null;
    }

    /// <summary>
    /// Computes the code of a time step; exposed so tests can produce valid codes.
    /// </summary>
    /// <param name="secret">Base32 shared secret.</param>
    /// <param name="step">Time step (seconds since the Unix epoch divided by thirty).</param>
    /// <returns>The six-digit code.</returns>
    public static string ComputeCode(string secret, long step)
    {
        if (!TryDecodeSecret(secret, out var key))
        {
            throw new ArgumentException("The secret is not valid Base32.", nameof(secret));
        }

        var instant = DateTimeOffset.FromUnixTimeSeconds(step * StepSeconds).UtcDateTime;
        return CreateTotp(key).ComputeTotp(instant);
    }

    /// <summary>
    /// Gets the time step that corresponds to the supplied instant.
    /// </summary>
    /// <param name="instant">The instant to convert.</param>
    /// <returns>The time step.</returns>
    public static long StepOf(DateTimeOffset instant)
    {
        return instant.ToUnixTimeSeconds() / StepSeconds;
    }

    private static Totp CreateTotp(byte[] key)
    {
        return new Totp(key, StepSeconds, OtpHashMode.Sha1, Digits);
    }

    private static bool TryNormalizeCode(string code, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var digits = code.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();
        if (digits.Length != Digits || !digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        normalized = digits;
        return true;
    }

    private static bool TryDecodeSecret(string secret, out byte[] key)
    {
        key = [];
        if (string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        try
        {
            key = Base32Encoding.ToBytes(
                secret.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant()
            );
        }
        catch (ArgumentException)
        {
            return false;
        }

        return key.Length > 0;
    }
}
