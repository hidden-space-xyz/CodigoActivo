namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Defines the per-recipient budgets and the budget of mail anyone can ask for. The email guard,
/// the <see cref="EmailSendLimiter"/> that <see cref="ThrottledEmailSender"/> applies before storing
/// a code, spends them; the outbox deliverer paces notices and administrator-written mail with the
/// same per-recipient budgets. The values are fixed in code, and every API process keeps its
/// budgets in memory, so a restart refills them and replicas multiply them.
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
    /// Gets or sets the capacity of the budget the mail anyone can ask for spends from: the
    /// verification of a registration or a new email, the notice that an address already has an
    /// account and the password reset. It applies through <see cref="AnonymousBudget"/>.
    /// </summary>
    public int AnonymousBurst { get; set; } = 1000;

    /// <summary>
    /// Gets or sets how many messages per hour the anonymous budget gets back. It applies through
    /// <see cref="AnonymousBudget"/>.
    /// </summary>
    public int AnonymousPerHour { get; set; } = 1000;

    /// <summary>
    /// Gets or sets how many address budgets each limiter keeps in memory. Once it holds this many, a
    /// new address is not limited on its own and the guard reports
    /// <see cref="EmailGuardAlert.TrackingSaturated"/>.
    /// </summary>
    public int MaxTrackedRecipients { get; set; } = 50_000;

    /// <summary>
    /// Gets or sets how often a limiter drops the address budgets that are full again and so hold
    /// nothing worth keeping. A sweep also runs early when <see cref="MaxTrackedRecipients"/> is
    /// reached and the previous sweep freed room.
    /// </summary>
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the minimum interval between two log entries of the same repeated email alert
    /// or outbox failure.
    /// </summary>
    public TimeSpan AlertInterval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Gets the anonymous budget actually applied: <see cref="AnonymousBurst"/> and
    /// <see cref="AnonymousPerHour"/>, each kept at or below half of what the SMTP provider delivers
    /// in an hour, so mail anyone can ask for never takes more than half of the delivery.
    /// </summary>
    /// <param name="providerPerMinute">Messages the SMTP provider accepts per minute.</param>
    /// <returns>The capacity and hourly refill of the anonymous budget.</returns>
    public (int Burst, int PerHour) AnonymousBudget(int providerPerMinute)
    {
        var half = (int)Math.Min(int.MaxValue, Math.Max(providerPerMinute, 1) * 60L / 2);
        return (Math.Min(AnonymousBurst, half), Math.Min(AnonymousPerHour, half));
    }
}
