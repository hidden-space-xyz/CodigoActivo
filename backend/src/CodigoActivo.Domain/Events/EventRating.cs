using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Events;

/// <summary>
/// Anonymous rating of an event given by one of its participants: a score and optional answers.
/// </summary>
public class EventRating : IdentifiableEntity, IAggregateRoot
{
    /// <summary>
    /// Lowest score allowed.
    /// </summary>
    public const int MinScore = 0;

    /// <summary>
    /// Highest score allowed.
    /// </summary>
    public const int MaxScore = 5;

    /// <summary>
    /// Maximum length of each answer.
    /// </summary>
    public const int MaxAnswerLength = 2000;

    private EventRating() { }

    /// <summary>
    /// Gets the identifier of the rated event.
    /// </summary>
    public Guid EventId { get; private set; }

    /// <summary>
    /// Gets the score.
    /// </summary>
    public int Score { get; private set; }

    /// <summary>
    /// Gets what the participant liked most.
    /// </summary>
    public string? MostLiked { get; private set; }

    /// <summary>
    /// Gets what the participant liked least.
    /// </summary>
    public string? LeastLiked { get; private set; }

    /// <summary>
    /// Gets the suggestions.
    /// </summary>
    public string? Suggestions { get; private set; }

    /// <summary>
    /// Records a rating of an event. Blank answers are stored as missing.
    /// </summary>
    /// <param name="eventId">Identifier of the rated event.</param>
    /// <param name="score">Score.</param>
    /// <param name="mostLiked">What the participant liked most.</param>
    /// <param name="leastLiked">What the participant liked least.</param>
    /// <param name="suggestions">Suggestions.</param>
    /// <returns>The new rating.</returns>
    public static EventRating Submit(
        Guid eventId,
        int score,
        string? mostLiked,
        string? leastLiked,
        string? suggestions
    )
    {
        return new EventRating
        {
            EventId = eventId,
            Score = score,
            MostLiked = mostLiked.NormalizeOrNull(),
            LeastLiked = leastLiked.NormalizeOrNull(),
            Suggestions = suggestions.NormalizeOrNull(),
        };
    }
}
