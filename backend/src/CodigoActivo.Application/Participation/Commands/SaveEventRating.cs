using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

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
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
public sealed class SaveEventRatingCommandHandler(
    IEventRepository events,
    IEventRatingRepository ratings,
    IActivityRepository activities,
    IQueryExecutor executor,
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
        var eventId = command.EventId;
        var userId = command.UserId;
        var request = command.Request;

        var ends = await executor.FirstOrDefaultAsync(
            events.Query().Where(e => e.Id == eventId).Select(e => (DateOnly?)e.EventEndsAt),
            ct
        );
        if (ends is not { } eventEndsAt)
        {
            return Error.NotFound(ErrorCode.EventNotFound);
        }

        if (eventEndsAt >= clock.Today)
        {
            return Error.Conflict(ErrorCode.EventRatingNotFinished);
        }

        var attended = await executor.FirstOrDefaultAsync(
            activities
                .QueryAssignments()
                .Where(a =>
                    a.Activity.EventId == eventId
                    && a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed
                    && (a.UserId == userId || a.User.ParentId == userId)
                )
                .Select(a => (Guid?)a.ActivityId),
            ct
        );
        if (attended is null)
        {
            return Error.Conflict(ErrorCode.EventRatingAttendanceRequired);
        }

        var rating = new EventRating { EventId = eventId };
        rating.Apply(
            request.Score!.Value,
            request.MostLiked,
            request.LeastLiked,
            request.Suggestions
        );

        await ratings.AddAsync(rating, ct);
        await uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
