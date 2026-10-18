namespace CodigoActivo.Domain.Users;

/// <summary>
/// A change of the news consent of an erased account, as the legal copy keeps it to prove
/// the consent behind the news emails it received.
/// </summary>
/// <param name="Granted">Whether the consent was given; <see langword="false"/> when withdrawn.</param>
/// <param name="Source">Where the holder decided.</param>
/// <param name="ChangedAt">When the holder decided.</param>
public sealed record LegalCopyConsentChange(
    bool Granted,
    NewsConsentSource Source,
    DateTimeOffset ChangedAt
);
