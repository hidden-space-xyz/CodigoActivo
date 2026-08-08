using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Events.Queries;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Application.Events.Commands;

public sealed record UpdateEventCommand(Guid EventId, UpdateEventRequest Request, Guid UserId)
    : ICommand<Result<EventResponse>>;

public sealed class UpdateEventCommandHandler(
    IEventRepository events,
    IActivityRepository activities,
    IFileRepository files,
    IOrphanFileCleaner orphanCleaner,
    EventCategoryChecker categoryChecker,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator,
    GetEventByIdQueryHandler getById
) : ICommandHandler<UpdateEventCommand, Result<EventResponse>>
{
    public async Task<Result<EventResponse>> HandleAsync(
        UpdateEventCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        var schedule = EventRules.ValidateSchedule(
            request.EventStartsAt,
            request.EventEndsAt,
            request.EarlySignupStartsAt,
            request.SignupStartsAt,
            request.SignupEndsAt
        );
        if (schedule.IsFailure)
        {
            return schedule.Error!;
        }

        var categories = await categoryChecker.EnsureCategoriesAsync(request.CategoryTypeIds, ct);
        if (categories.IsFailure)
        {
            return categories.Error!;
        }

        var ev = await events.GetForEditAsync(command.EventId, ct);
        if (ev is null)
        {
            return Error.NotFound(ErrorCode.EventNotFound);
        }

        var (lowerInclusive, upperExclusive) = DayBounds(
            schedule.Value.EventStartsAt,
            schedule.Value.EventEndsAt
        );
        if (
            await activities.AnyOutsideRangeAsync(
                command.EventId,
                lowerInclusive,
                upperExclusive,
                ct
            )
        )
        {
            return Error.BadRequest(ErrorCode.EventActivitiesOutsideNewRange);
        }

        if (!await files.ExistsAsync(f => f.Id == request.ThumbnailId, ct))
        {
            return Error.BadRequest(ErrorCode.EventThumbnailNotFound);
        }

        var previousThumbnailId = ev.ThumbnailId;
        var previousDescription = ev.Description;

        ev.Title = request.Title.Trim();
        ev.Subtitle = request.Subtitle.Trim();
        ev.Description = request.Description;
        ev.EventStartsAt = schedule.Value.EventStartsAt;
        ev.EventEndsAt = schedule.Value.EventEndsAt;
        ev.EarlySignupStartsAt = schedule.Value.EarlySignupStartsAt;
        ev.SignupStartsAt = schedule.Value.SignupStartsAt;
        ev.SignupEndsAt = schedule.Value.SignupEndsAt;
        ev.ThumbnailId = request.ThumbnailId;
        ev.UpdatedAt = clock.UtcNow;
        ev.UpdatedBy = command.UserId;

        EventRules.SyncCategories(ev, request.CategoryTypeIds!);

        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Events);

        var orphanCandidates = RichTextFileReferences
            .ExtractRemoved(previousDescription, ev.Description)
            .ToList();
        if (previousThumbnailId != request.ThumbnailId)
        {
            orphanCandidates.Add(previousThumbnailId);
        }

        await orphanCleaner.DeleteOrphanedAsync(orphanCandidates, ct);

        return await getById.HandleAsync(new GetEventByIdQuery(command.EventId), ct);
    }

    private (DateTimeOffset LowerInclusive, DateTimeOffset UpperExclusive) DayBounds(
        DateOnly eventStart,
        DateOnly eventEnd
    )
    {
        return (
            LocalDayRange.LowerUtc(eventStart, clock.TimeZone),
            LocalDayRange.UpperExclusiveUtc(eventEnd, clock.TimeZone)
        );
    }
}
