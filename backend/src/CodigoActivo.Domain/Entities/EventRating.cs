using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Represents the persisted event rating domain entity and its relationships.
/// </summary>
public class EventRating : IdentifiableEntity
{
    /// <summary>
    /// Identifies the min score configuration or policy value.
    /// </summary>
    public const int MinScore = 0;

    /// <summary>
    /// Identifies the max score configuration or policy value.
    /// </summary>
    public const int MaxScore = 5;

    /// <summary>
    /// Identifies the max answer length configuration or policy value.
    /// </summary>
    public const int MaxAnswerLength = 2000;

    /// <summary>
    /// Gets or sets the identifier of the associated event.
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// Gets or sets the associated event.
    /// </summary>
    public Event Event { get; set; } = null!;

    /// <summary>
    /// Gets or sets the score value.
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// Gets or sets the most liked value.
    /// </summary>
    public string? MostLiked { get; set; }

    /// <summary>
    /// Gets or sets the least liked value.
    /// </summary>
    public string? LeastLiked { get; set; }

    /// <summary>
    /// Gets or sets the suggestions value.
    /// </summary>
    public string? Suggestions { get; set; }

    /// <summary>
    /// Applies the event rating rules to the supplied target.
    /// </summary>
    /// <param name="score">The score value.</param>
    /// <param name="mostLiked">The most liked value.</param>
    /// <param name="leastLiked">The least liked value.</param>
    /// <param name="suggestions">The suggestions value.</param>
    public void Apply(int score, string? mostLiked, string? leastLiked, string? suggestions)
    {
        Score = score;
        MostLiked = Normalize(mostLiked);
        LeastLiked = Normalize(leastLiked);
        Suggestions = Normalize(suggestions);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
