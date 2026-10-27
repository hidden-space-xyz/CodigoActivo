namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Identifies the condition of the email guard that its caller has to log. Each one is reported at most
/// once per <see cref="EmailGuardOptions.AlertInterval"/> while it repeats.
/// </summary>
public enum EmailGuardAlert
{
    /// <summary>
    /// Nothing to log, either because nothing happened or because the alert was already reported
    /// within the interval.
    /// </summary>
    None,

    /// <summary>
    /// The budget of the mail anyone can ask for denied a message.
    /// </summary>
    GlobalBudgetExhausted,

    /// <summary>
    /// The guard already holds <see cref="EmailGuardOptions.MaxTrackedRecipients"/> address budgets,
    /// so a new address was accepted without a budget of its own.
    /// </summary>
    TrackingSaturated,
}
