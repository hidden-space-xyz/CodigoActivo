using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Participation.Contracts;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;

namespace CodigoActivo.Application.Participation.Commands;

/// <summary>
/// Carries the input required to save event rating.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="Request">Validated client request data.</param>
public sealed record SaveEventRatingCommand(
    Guid EventId,
    Guid UserId,
    SaveEventRatingRequest Request
) : ICommand<Result>;

/// <summary>
/// Executes the command to save event rating. Every accepted call appends one anonymous rating row:
/// nothing records who wrote it, so an attendee may rate the same event more than once and no
/// answer is ever overwritten.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="ratings">Repository used to persist and retrieve ratings.</param>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
public sealed class SaveEventRatingCommandHandler(
    IEventRepository events,
    IEventRatingRepository ratings,
    IActivityRepository activities,
    IClock clock,
    IUnitOfWork uow
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
        var request = command.Request;

        var ev = await events.GetByIdAsync(command.EventId, ct);
        if (ev is null)
        {
            return Error.NotFound(ErrorCode.EventNotFound);
        }

        if (!ev.HasEndedBy(clock.Today))
        {
            return Error.Conflict(ErrorCode.EventRatingNotFinished);
        }

        if (!await activities.HasConfirmedAttendanceAsync(ev.Id, command.UserId, ct))
        {
            return Error.Conflict(ErrorCode.EventRatingAttendanceRequired);
        }

        var rating = EventRating.Submit(
            ev.Id,
            request.Score,
            request.MostLiked,
            request.LeastLiked,
            request.Suggestions
        );
        if (rating.IsFailure)
        {
            return rating.Error!;
        }

        await ratings.AddAsync(rating.Value, ct);
        await uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
