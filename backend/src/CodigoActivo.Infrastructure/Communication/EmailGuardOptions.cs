namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Defines the limits of the email guard, the <see cref="EmailSendLimiter"/> that
/// <see cref="ThrottledEmailSender"/> applies to automatic mail before storing it; administrator-written
/// mail goes to the outbox directly and spends none of them. The values are fixed in code, and every API
/// process keeps its budgets in memory, so a restart refills them and replicas multiply them.
/// </summary>
public sealed class EmailGuardOptions
{
    /// <summary>
    /// Gets or sets how many messages one normalized address can receive at once: the capacity of its
    /// hourly budget, which refills at <see cref="RecipientPerHour"/>. Second-factor codes have budgets
    /// of their own per address.
    /// </summary>
    public int RecipientBurst { get; set; } = 20;

    /// <summary>
    /// Gets or sets how many messages per hour the hourly budget of an address gets back.
    /// </summary>
    public int RecipientPerHour { get; set; } = 10;

    /// <summary>
    /// Gets or sets how many messages one address can receive per day: the capacity of its daily
    /// budget, which gets them back spread evenly over 24 hours.
    /// </summary>
    public int RecipientPerDay { get; set; } = 50;

    /// <summary>
    /// Gets or sets the capacity of the global budget that every message of the process spends from.
    /// </summary>
    public int GlobalBurst { get; set; } = 1000;

    /// <summary>
    /// Gets or sets how many messages per hour the global budget gets back.
    /// </summary>
    public int GlobalPerHour { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the part of the global budget kept for second-factor, verification and
    /// password-reset mail: any other message is denied when sending it would leave less than this.
    /// It applies through <see cref="EffectiveCredentialReserve"/>.
    /// </summary>
    public int GlobalCredentialReserve { get; set; } = 200;

    /// <summary>
    /// Gets the credential reserve actually applied: <see cref="GlobalCredentialReserve"/> kept between
    /// zero and one less than <see cref="GlobalBurst"/>, so a full global budget never denies a message.
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
    /// Gets or sets how many address budgets the guard keeps in memory. Once it holds this many, a new
    /// address spends only the global budget and the guard reports
    /// <see cref="EmailGuardAlert.TrackingSaturated"/>.
    /// </summary>
    public int MaxTrackedRecipients { get; set; } = 50_000;

    /// <summary>
    /// Gets or sets how often the guard drops the address budgets that are full again and so hold
    /// nothing worth keeping. A sweep also runs early when <see cref="MaxTrackedRecipients"/> is
    /// reached and the previous sweep freed room.
    /// </summary>
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the minimum interval between two log entries of the same repeated email alert
    /// or outbox failure.
    /// </summary>
    public TimeSpan AlertInterval { get; set; } = TimeSpan.FromMinutes(15);
}
