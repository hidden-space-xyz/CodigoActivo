using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Infrastructure.DataHistory;

/// <summary>
/// Defines how long the data history of each entity is kept. The default covers the longest
/// liability its rows serve: personal actions on the event conditions accepted (article 1964.2 of
/// the Civil Code, five years), which also outlasts the four years of image and honor claims
/// (article 9.5 LO 1/1982). News consents and ratings only serve the data protection
/// infringements, which prescribe in three years (article 72 LOPDGDD).
/// </summary>
public sealed class DataHistoryRetentionOptions
{
    /// <summary>
    /// Gets or sets the years an entry is kept when its entity has no specific retention.
    /// </summary>
    public int DefaultYears { get; set; } = 5;

    /// <summary>
    /// Gets the years an entry is kept, by entity name, when they differ from the default.
    /// </summary>
    public IReadOnlyDictionary<string, int> YearsByEntity { get; } =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [nameof(NewsConsentChange)] = 3,
            [nameof(EventRating)] = 3,
        };

    /// <summary>
    /// Gets or sets the interval between two purges.
    /// </summary>
    public TimeSpan PurgeInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the delay applied before the first purge, so startup work finishes first.
    /// </summary>
    public TimeSpan PurgeStartupDelay { get; set; } = TimeSpan.FromSeconds(30);
}
