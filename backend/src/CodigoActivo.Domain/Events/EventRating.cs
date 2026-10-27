using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.Events;

/// <summary>
/// Rating of an event given by the account of one of its participants: an optional score and
/// optional answers, never all of them missing. An account rates each event once and may revise
/// or delete its rating later.
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
    /// Gets the identifier of the account that wrote the rating.
    /// </summary>
    public UserId UserId { get; private set; }

    /// <summary>
    /// Gets the score, or <see langword="null"/> when the participant only answered the questions.
    /// </summary>
    public int? Score { get; private set; }

    /// <summary>
    /// Gets what the participant liked most, or <see langword="null"/> when they did not answer.
    /// </summary>
    public string? MostLiked { get; private set; }

    /// <summary>
    /// Gets what the participant liked least, or <see langword="null"/> when they did not answer.
    /// </summary>
    public string? LeastLiked { get; private set; }

    /// <summary>
    /// Gets the participant's suggestions, or <see langword="null"/> when they did not answer.
    /// </summary>
    public string? Suggestions { get; private set; }

    /// <summary>
    /// Records the rating of an event by an account, under the rules of <see cref="Revise"/>.
    /// </summary>
    /// <param name="eventId">Identifier of the rated event.</param>
    /// <param name="userId">Identifier of the account that writes the rating.</param>
    /// <param name="score">Score, or <see langword="null"/> when the participant gave none.</param>
    /// <param name="mostLiked">What the participant liked most.</param>
    /// <param name="leastLiked">What the participant liked least.</param>
    /// <param name="suggestions">Suggestions of the participant.</param>
    /// <returns>The new rating, or a validation error naming the rule it breaks.</returns>
    public static Result<EventRating> Submit(
        EventId eventId,
        UserId userId,
        int? score,
        string? mostLiked,
        string? leastLiked,
        string? suggestions
    )
    {
        var rating = new EventRating { EventId = eventId, UserId = userId };
        var revised = rating.Revise(score, mostLiked, leastLiked, suggestions);
        return revised.IsFailure ? revised.Error! : rating;
    }

    /// <summary>
    /// Replaces the score and the answers. Blank answers are stored as missing; a score outside
    /// <see cref="MinScore"/> to <see cref="MaxScore"/>, an answer longer than
    /// <see cref="MaxAnswerLength"/> and a rating without a score or any answer are refused and
    /// change nothing.
    /// </summary>
    /// <param name="score">Score, or <see langword="null"/> when the participant gives none.</param>
    /// <param name="mostLiked">What the participant liked most.</param>
    /// <param name="leastLiked">What the participant liked least.</param>
    /// <param name="suggestions">Suggestions of the participant.</param>
    /// <returns>
    /// Success, or <see cref="DomainErrorCode.EventRatingScoreOutOfRange"/>,
    /// <see cref="DomainErrorCode.EventRatingAnswerTooLong"/> or
    /// <see cref="DomainErrorCode.EventRatingEmpty"/> for the rule it breaks.
    /// </returns>
    public Result Revise(int? score, string? mostLiked, string? leastLiked, string? suggestions)
    {
        if (score is < MinScore or > MaxScore)
        {
            return Error.Validation(DomainErrorCode.EventRatingScoreOutOfRange);
        }

        var most = mostLiked.NormalizeOrNull();
        var least = leastLiked.NormalizeOrNull();
        var suggested = suggestions.NormalizeOrNull();
        if (
            most?.Length > MaxAnswerLength
            || least?.Length > MaxAnswerLength
            || suggested?.Length > MaxAnswerLength
        )
        {
            return Error.Validation(DomainErrorCode.EventRatingAnswerTooLong);
        }

        if (score is null && most is null && least is null && suggested is null)
        {
            return Error.Validation(DomainErrorCode.EventRatingEmpty);
        }

        Score = score;
        MostLiked = most;
        LeastLiked = least;
        Suggestions = suggested;
        return Result.Success();
    }
}
