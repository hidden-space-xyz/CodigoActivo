namespace CodigoActivo.Domain.Users;

/// <summary>
/// One change of the promotional consent of an account: what the holder decided, when and where.
/// The account keeps every change, so the consent behind each promotional email can be proven;
/// part of the user aggregate.
/// </summary>
public class PromotionalConsentChange
{
    private PromotionalConsentChange() { }

    internal PromotionalConsentChange(
        UserId userId,
        bool granted,
        PromotionalConsentSource source,
        DateTimeOffset changedAt
    )
    {
        UserId = userId;
        Granted = granted;
        Source = source;
        ChangedAt = changedAt;
    }

    /// <summary>
    /// Gets the identifier of the account.
    /// </summary>
    public UserId UserId { get; private set; }

    /// <summary>
    /// Gets whether the holder gave the consent; <see langword="false"/> when they withdrew it.
    /// </summary>
    public bool Granted { get; private set; }

    /// <summary>
    /// Gets where the holder decided.
    /// </summary>
    public PromotionalConsentSource Source { get; private set; }

    /// <summary>
    /// Gets when the holder decided.
    /// </summary>
    public DateTimeOffset ChangedAt { get; private set; }
}
