namespace CodigoActivo.Domain.Users;

/// <summary>
/// A change of the promotional consent of an erased account, as the legal copy keeps it to prove
/// the consent behind the promotional emails it received.
/// </summary>
/// <param name="Granted">Whether the consent was given; <see langword="false"/> when withdrawn.</param>
/// <param name="Source">Where the holder decided.</param>
/// <param name="ChangedAt">When the holder decided.</param>
public sealed record LegalCopyConsentChange(
    bool Granted,
    PromotionalConsentSource Source,
    DateTimeOffset ChangedAt
);
