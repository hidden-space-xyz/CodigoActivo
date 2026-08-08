using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Participation.Commands;

public sealed record SaveEventRatingCommand(
    Guid EventId,
    Guid UserId,
    SaveEventRatingRequest Request
) : ICommand<Result<EventRatingResponse>>;

public sealed class SaveEventRatingCommandHandler(
    IEventRepository events,
    IEventRatingRepository ratings,
    IActivityRepository activities,
    IQueryExecutor executor,
    IUnitOfWork unitOfWork,
    IClock clock
) : ICommandHandler<SaveEventRatingCommand, Result<EventRatingResponse>>
{
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
