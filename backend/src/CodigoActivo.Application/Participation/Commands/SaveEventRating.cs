using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;

namespace CodigoActivo.Application.Participation.Commands;

/// <summary>
/// Carries the input required to save event rating.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="Score">Score from 1 to 5, if any.</param>
/// <param name="MostLiked">What the participant liked most, if anything.</param>
/// <param name="LeastLiked">What the participant liked least, if anything.</param>
/// <param name="Suggestions">Suggestions, if any.</param>
public sealed record SaveEventRatingCommand(
    EventId EventId,
    [property: Range(EventRating.MinScore, EventRating.MaxScore)] int? Score,
    [property: MaxLength(EventRating.MaxAnswerLength)] string? MostLiked,
    [property: MaxLength(EventRating.MaxAnswerLength)] string? LeastLiked,
    [property: MaxLength(EventRating.MaxAnswerLength)] string? Suggestions
) : ICommand<Result>;

/// <summary>
/// Executes the command to save event rating. Every accepted call appends one anonymous rating row:
/// nothing records who wrote it, so an attendee may rate the same event more than once and no
/// answer is ever overwritten. Only someone who attended the event may rate it.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="ratings">Repository used to persist and retrieve ratings.</param>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class SaveEventRatingCommandHandler(
    IEventRepository events,
    IEventRatingRepository ratings,
    IActivityRepository activities,
    ICurrentUser currentUser,
    IClock clock
) : ICommandHandler<SaveEventRatingCommand, Result>
{
    /// <summary>
    /// Handles the request to save event rating.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(
        SaveEventRatingCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var ev = await events.GetByIdAsync(command.EventId, ct);
        if (ev is null)
        {
            return Error.NotFound(ApplicationErrorCode.EventNotFound);
        }

        if (!ev.HasEndedBy(clock.Today))
        {
            return Error.Conflict(ApplicationErrorCode.EventRatingNotFinished);
        }

        if (!await activities.HasConfirmedAttendanceAsync(ev.Id, currentUser.RequiredId(), ct))
        {
            return Error.Conflict(ApplicationErrorCode.EventRatingAttendanceRequired);
        }

        var rating = EventRating.Submit(
            ev.Id,
            command.Score,
            command.MostLiked,
            command.LeastLiked,
            command.Suggestions
        );
        if (rating.IsFailure)
        {
            return rating.Error!;
        }

        await ratings.AddAsync(rating.Value, ct);
        return Result.Success();
    }
}
