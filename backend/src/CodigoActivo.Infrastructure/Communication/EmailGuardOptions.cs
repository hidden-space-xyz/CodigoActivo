namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Defines configuration values for email guard.
/// </summary>
public sealed class EmailGuardOptions
{
    /// <summary>
    /// Gets or sets the recipient burst value.
    /// </summary>
    public int RecipientBurst { get; set; } = 20;

    /// <summary>
    /// Gets or sets the recipient per hour value.
    /// </summary>
    public int RecipientPerHour { get; set; } = 10;

    /// <summary>
    /// Gets or sets the recipient per day value.
    /// </summary>
    public int RecipientPerDay { get; set; } = 50;

    /// <summary>
    /// Gets or sets the global burst value.
    /// </summary>
    public int GlobalBurst { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the global per hour value.
    /// </summary>
    public int GlobalPerHour { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the global credential reserve value.
    /// </summary>
    public int GlobalCredentialReserve { get; set; } = 200;

    /// <summary>
    /// Gets the effective credential reserve value.
    /// </summary>
    public int EffectiveCredentialReserve =>
        Math.Clamp(GlobalCredentialReserve, 0, Math.Max(GlobalBurst - 1, 0));

    /// <summary>
    /// Gets the half of the credential reserve kept for second-factor codes alone: verification and
    /// password-reset mail, which anyone can trigger, cannot spend it, so they never use up the
    /// budget a login needs.
    /// </summary>
    public int EffectiveTwoFactorReserve => EffectiveCredentialReserve / 2;

    /// <summary>
    /// Gets or sets the max tracked recipients value.
    /// </summary>
    public int MaxTrackedRecipients { get; set; } = 50_000;

    /// <summary>
    /// Gets or sets the sweep interval value.
    /// </summary>
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the minimum interval between two log entries of the same repeated email alert
    /// or outbox failure.
    /// </summary>
    public TimeSpan AlertInterval { get; set; } = TimeSpan.FromMinutes(15);
}
