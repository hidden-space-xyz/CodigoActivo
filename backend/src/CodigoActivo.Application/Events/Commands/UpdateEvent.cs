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

/// <summary>
/// Carries the input required to update the event.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="Request">Validated client request data.</param>
/// <param name="UserId">Identifier of the user.</param>
public sealed record UpdateEventCommand(Guid EventId, UpdateEventRequest Request, Guid UserId)
    : ICommand<Result<EventResponse>>;

/// <summary>
/// Executes the command to update the event.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="termsDocuments">Repository used to persist and retrieve terms documents.</param>
/// <param name="orphanCleaner">Service used to remove files that are no longer referenced.</param>
/// <param name="categoryChecker">The category checker value.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
/// <param name="getById">Handler used to retrieve event by identifier.</param>
public sealed class UpdateEventCommandHandler(
    IEventRepository events,
    IActivityRepository activities,
    IFileRepository files,
    ITermsDocumentRepository termsDocuments,
    IOrphanFileCleaner orphanCleaner,
    EventCategoryChecker categoryChecker,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator,
    GetEventByIdQueryHandler getById
) : ICommandHandler<UpdateEventCommand, Result<EventResponse>>
{
    /// <summary>
    /// Handles the request to update the event.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an event on success, or an application error on failure.</returns>
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

        if (request.TermsDocuments is { Count: > 0 } termsDocumentRequests)
        {
            var termsValidation = await ValidateTermsDocumentsAsync(termsDocumentRequests, ct);
            if (termsValidation.IsFailure)
            {
                return termsValidation.Error!;
            }
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
        EventRules.SyncTermsDocuments(ev, request.TermsDocuments);

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

    private async Task<Result> ValidateTermsDocumentsAsync(
        IReadOnlyList<EventTermsDocumentRequest> termsDocumentRequests,
        CancellationToken ct
    )
    {
        var ids = termsDocumentRequests.Select(t => t.TermsDocumentId).ToList();
        var distinctIds = ids.Distinct().ToList();
        if (distinctIds.Count != ids.Count)
        {
            return Error.BadRequest(ErrorCode.EventTermsDocumentDuplicated);
        }

        var existingCount = await termsDocuments.CountAsync(t => distinctIds.Contains(t.Id), ct);
        return existingCount != distinctIds.Count
            ? (Result)Error.BadRequest(ErrorCode.TermsDocumentNotFound)
            : Result.Success();
    }
}
