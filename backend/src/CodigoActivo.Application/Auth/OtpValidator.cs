using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Security;

namespace CodigoActivo.Application.Auth;

/// <summary>
/// Validates otp input before it is processed.
/// </summary>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="hasher">The hasher value.</param>
public sealed class OtpValidator(IClock clock, IPasswordHasher hasher)
{
    /// <summary>
    /// Determines whether code valid.
    /// </summary>
    /// <param name="code">The code value.</param>
    /// <param name="codeHash">The code hash value.</param>
    /// <param name="expiresAt">The expires at value.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public bool IsCodeValid(string code, string? codeHash, DateTimeOffset? expiresAt)
    {
        return !string.IsNullOrWhiteSpace(code)
            && codeHash is not null
            && expiresAt >= clock.UtcNow
            && hasher.Verify(code.Trim().ToLowerInvariant(), codeHash);
    }
}
