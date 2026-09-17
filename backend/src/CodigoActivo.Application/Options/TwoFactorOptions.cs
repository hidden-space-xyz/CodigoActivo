namespace CodigoActivo.Application.Options;

/// <summary>
/// Defines configuration values for the mandatory second login factor.
/// </summary>
public sealed class TwoFactorOptions
{
    /// <summary>
    /// Stores the shared default challenge lifetime value.
    /// </summary>
    public static readonly TimeSpan DefaultChallengeLifetime = TimeSpan.FromMinutes(10);
    /// <summary>
    /// Stores the shared default resend cooldown value.
    /// </summary>
    public static readonly TimeSpan DefaultResendCooldown = TimeSpan.FromSeconds(60);
    /// <summary>
    /// Stores the shared default authenticator setup lifetime value.
    /// </summary>
    public static readonly TimeSpan DefaultSetupLifetime = TimeSpan.FromMinutes(15);
    /// <summary>
    /// Stores the shared default lockout duration value.
    /// </summary>
    public static readonly TimeSpan DefaultLockoutDuration = TimeSpan.FromMinutes(15);
    /// <summary>
    /// Stores the shared default maximum failed attempts value.
    /// </summary>
    public const int DefaultMaxFailedAttempts = 5;
    /// <summary>
    /// Stores the shared default issuer shown by authenticator applications.
    /// </summary>
    public const string DefaultIssuer = "Código Activo";

    /// <summary>
    /// Gets or sets how long a password-verified login may wait for its second factor. Emailed
    /// codes share this lifetime.
    /// </summary>
    public TimeSpan ChallengeLifetime { get; set; } = DefaultChallengeLifetime;

    /// <summary>
    /// Gets or sets the minimum time between two emailed login codes for the same account.
    /// </summary>
    public TimeSpan ResendCooldown { get; set; } = DefaultResendCooldown;

    /// <summary>
    /// Gets or sets how long an authenticator enrollment can be confirmed.
    /// </summary>
    public TimeSpan SetupLifetime { get; set; } = DefaultSetupLifetime;

    /// <summary>
    /// Gets or sets the wrong codes tolerated before second-factor verification locks.
    /// </summary>
    public int MaxFailedAttempts { get; set; } = DefaultMaxFailedAttempts;

    /// <summary>
    /// Gets or sets how long second-factor verification stays locked after too many failures.
    /// </summary>
    public TimeSpan LockoutDuration { get; set; } = DefaultLockoutDuration;

    /// <summary>
    /// Gets or sets the issuer name embedded in authenticator enrollment URIs.
    /// </summary>
    public string Issuer { get; set; } = DefaultIssuer;
}
