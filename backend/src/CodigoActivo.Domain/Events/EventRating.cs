using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Events;

/// <summary>
/// Anonymous rating of an event given by one of its participants: an optional score and optional
/// answers, never all of them missing.
/// </summary>
public class EventRating : AggregateRoot<EventRatingId>
{
    /// <summary>
    /// Lowest score allowed.
    /// </summary>
    public const int MinScore = 1;

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
    public EventId EventId { get; private set; }

    /// <summary>
    /// Gets the score, or <see langword="null"/> when the participant only answered the questions.
    /// </summary>
    public int? Score { get; private set; }

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
    /// Records a rating of an event. Blank answers are stored as missing, and a rating without a score
    /// or any answer is refused.
    /// </summary>
    /// <param name="eventId">Identifier of the rated event.</param>
    /// <param name="score">Score, or <see langword="null"/> when the participant gave none.</param>
    /// <param name="mostLiked">What the participant liked most.</param>
    /// <param name="leastLiked">What the participant liked least.</param>
    /// <param name="suggestions">Suggestions.</param>
    /// <returns>The new rating, or <see cref="DomainErrorCode.EventRatingEmpty"/> when it carries nothing.</returns>
    public static Result<EventRating> Submit(
        EventId eventId,
        int? score,
        string? mostLiked,
        string? leastLiked,
        string? suggestions
    )
    {
        var rating = new EventRating
        {
            EventId = eventId,
            Score = score,
            MostLiked = mostLiked.NormalizeOrNull(),
            LeastLiked = leastLiked.NormalizeOrNull(),
            Suggestions = suggestions.NormalizeOrNull(),
        };

        return
            rating.Score is null
            && rating.MostLiked is null
            && rating.LeastLiked is null
            && rating.Suggestions is null
            ? Error.Validation(DomainErrorCode.EventRatingEmpty)
            : rating;
    }
}
