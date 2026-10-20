namespace CodigoActivo.Application.Accounts.SecondFactor;

/// <summary>
/// Defines configuration values for the mandatory second login factor.
/// </summary>
public sealed class TwoFactorOptions
{
    /// <summary>
    /// Gets or sets how long a password-verified login may wait for its second factor. Emailed
    /// codes share this lifetime.
    /// </summary>
    public TimeSpan ChallengeLifetime { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets or sets the minimum time between two emailed login codes for the same account.
    /// </summary>
    public TimeSpan ResendCooldown { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets or sets how long an authenticator enrollment can be confirmed.
    /// </summary>
    public TimeSpan SetupLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Gets or sets the wrong codes tolerated before second-factor verification locks.
    /// </summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets how long second-factor verification stays locked after too many failures.
    /// </summary>
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);
}
