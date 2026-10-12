namespace CodigoActivo.Domain.Users;

/// <summary>
/// A code emailed to an account, kept only as its hash, with when it expires and when it was last
/// sent. The verification, password reset and login codes each hold one; the default value holds
/// no code.
/// </summary>
/// <param name="Hash">Hash of the code, or <see langword="null"/> when there is none.</param>
/// <param name="ExpiresAt">When the code stops being accepted.</param>
/// <param name="LastSentAt">When the code was last sent, which the resend cooldown counts from.</param>
public readonly record struct OneTimeCode(
    string? Hash,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastSentAt
)
{
    /// <summary>
    /// Creates a code sent now that stays valid for its lifetime.
    /// </summary>
    /// <param name="hash">Hash of the code that was sent.</param>
    /// <param name="now">Current timestamp, stored as the sending time.</param>
    /// <param name="lifetime">How long the code stays valid.</param>
    /// <returns>The code.</returns>
    public static OneTimeCode Issue(string hash, DateTimeOffset now, TimeSpan lifetime)
    {
        return new OneTimeCode(hash, now + lifetime, now);
    }

    /// <summary>
    /// Gets the hash while the code can still be used.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <returns>The hash, or <see langword="null"/> when there is no code or it expired.</returns>
    public string? UsableHash(DateTimeOffset now)
    {
        return ExpiresAt >= now ? Hash : null;
    }

    /// <summary>
    /// Tells whether a new code must wait for the resend cooldown.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <param name="cooldown">Minimum time between two codes.</param>
    /// <returns><see langword="true"/> while the last code is too recent.</returns>
    public bool IsResendCoolingDown(DateTimeOffset now, TimeSpan cooldown)
    {
        return now < LastSentAt + cooldown;
    }

    /// <summary>
    /// Tells whether the code is still valid and was sent recently enough that a new one should
    /// not be issued yet.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <param name="resendCooldown">Minimum time between two codes.</param>
    /// <returns><see langword="true"/> when the existing code can still be used.</returns>
    public bool IsRecent(DateTimeOffset now, TimeSpan resendCooldown)
    {
        return Hash is not null && ExpiresAt > now && IsResendCoolingDown(now, resendCooldown);
    }

    /// <summary>
    /// Forgets that the code was sent, so a new one can be requested at once.
    /// </summary>
    /// <returns>The same code without its sending time.</returns>
    public OneTimeCode WithoutDelivery()
    {
        return this with { LastSentAt = null };
    }
}
