namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Defines configuration values for email guard.
/// </summary>
public sealed class EmailGuardOptions
{
    /// <summary>
    /// Identifies the default recipient burst configuration or policy value.
    /// </summary>
    public const int DefaultRecipientBurst = 20;
    /// <summary>
    /// Identifies the default recipient per hour configuration or policy value.
    /// </summary>
    public const int DefaultRecipientPerHour = 10;
    /// <summary>
    /// Identifies the default recipient per day configuration or policy value.
    /// </summary>
    public const int DefaultRecipientPerDay = 50;
    /// <summary>
    /// Identifies the default global burst configuration or policy value.
    /// </summary>
    public const int DefaultGlobalBurst = 1000;
    /// <summary>
    /// Identifies the default global per hour configuration or policy value.
    /// </summary>
    public const int DefaultGlobalPerHour = 1000;
    /// <summary>
    /// Identifies the default global credential reserve configuration or policy value.
    /// </summary>
    public const int DefaultGlobalCredentialReserve = 200;
    /// <summary>
    /// Identifies the default max tracked recipients configuration or policy value.
    /// </summary>
    public const int DefaultMaxTrackedRecipients = 50_000;

    /// <summary>
    /// Stores the shared default sweep interval value.
    /// </summary>
    public static readonly TimeSpan DefaultSweepInterval = TimeSpan.FromMinutes(5);
    /// <summary>
    /// Stores the shared default alert interval value.
    /// </summary>
    public static readonly TimeSpan DefaultAlertInterval = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Gets or sets the recipient burst value.
    /// </summary>
    public int RecipientBurst { get; set; } = DefaultRecipientBurst;

    /// <summary>
    /// Gets or sets the recipient per hour value.
    /// </summary>
    public int RecipientPerHour { get; set; } = DefaultRecipientPerHour;

    /// <summary>
    /// Gets or sets the recipient per day value.
    /// </summary>
    public int RecipientPerDay { get; set; } = DefaultRecipientPerDay;

    /// <summary>
    /// Gets or sets the global burst value.
    /// </summary>
    public int GlobalBurst { get; set; } = DefaultGlobalBurst;

    /// <summary>
    /// Gets or sets the global per hour value.
    /// </summary>
    public int GlobalPerHour { get; set; } = DefaultGlobalPerHour;

    /// <summary>
    /// Gets or sets the global credential reserve value.
    /// </summary>
    public int GlobalCredentialReserve { get; set; } = DefaultGlobalCredentialReserve;

    /// <summary>
    /// Gets the effective credential reserve value.
    /// </summary>
    public int EffectiveCredentialReserve =>
        Math.Clamp(GlobalCredentialReserve, 0, Math.Max(GlobalBurst - 1, 0));

    /// <summary>
    /// Gets or sets the max tracked recipients value.
    /// </summary>
    public int MaxTrackedRecipients { get; set; } = DefaultMaxTrackedRecipients;

    /// <summary>
    /// Gets or sets the sweep interval value.
    /// </summary>
    public TimeSpan SweepInterval { get; set; } = DefaultSweepInterval;

    /// <summary>
    /// Gets or sets the alert interval value.
    /// </summary>
    public TimeSpan AlertInterval { get; set; } = DefaultAlertInterval;
}
