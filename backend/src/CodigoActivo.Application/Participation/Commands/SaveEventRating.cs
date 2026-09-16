using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
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
) : ICommand<Result<EventRatingResponse>>;

/// <summary>
/// Executes the command to save event rating.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="ratings">Repository used to persist and retrieve ratings.</param>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="unitOfWork">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class SaveEventRatingCommandHandler(
    IEventRepository events,
    IEventRatingRepository ratings,
    IActivityRepository activities,
    IQueryExecutor executor,
    IUnitOfWork unitOfWork,
    IClock clock
) : ICommandHandler<SaveEventRatingCommand, Result<EventRatingResponse>>
{
    /// <summary>
    /// Handles the request to save event rating.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an event rating on success, or an application error on failure.</returns>
    public async Task<Result<EventRatingResponse>> HandleAsync(
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

        var now = clock.UtcNow;
        var rating = await ratings.FindAsync(r => r.EventId == eventId && r.UserId == userId, ct);

        if (rating is null)
        {
            rating = new EventRating
            {
                EventId = eventId,
                UserId = userId,
                CreatedAt = now,
            };
            await ratings.AddAsync(rating, ct);
        }
        else
        {
            rating.UpdatedAt = now;
        }

        rating.Apply(
            request.Score!.Value,
            request.MostLiked,
            request.LeastLiked,
            request.Suggestions
        );

        await unitOfWork.SaveChangesAsync(ct);
        return rating.ToResponse();
    }
}
