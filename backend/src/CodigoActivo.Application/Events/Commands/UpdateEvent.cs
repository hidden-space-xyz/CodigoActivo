using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Events.Contracts;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.TermsDocuments;

namespace CodigoActivo.Application.Events.Commands;

/// <summary>
/// Carries the input required to update the event.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="Request">Validated client request data.</param>
/// <param name="UserId">Identifier of the user.</param>
public sealed record UpdateEventCommand(Guid EventId, UpdateEventRequest Request, Guid UserId)
    : ICommand<Result>;

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
public sealed class UpdateEventCommandHandler(
    IEventRepository events,
    IActivityRepository activities,
    IFileRepository files,
    ITermsDocumentRepository termsDocuments,
    IOrphanFileCleaner orphanCleaner,
    EventCategoryChecker categoryChecker,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<UpdateEventCommand, Result>
{
    /// <summary>
    /// Handles the request to update the event.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or an application error on failure.</returns>
    public async Task<Result> HandleAsync(
        UpdateEventCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        var schedule = EventSchedule.Create(
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

        var ev = await events.GetByIdAsync(command.EventId, ct);
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
            return Error.Validation(ErrorCode.EventActivitiesOutsideNewRange);
        }

        if (!await files.ExistsAsync(request.ThumbnailId, ct))
        {
            return Error.Validation(ErrorCode.EventThumbnailNotFound);
        }

        var terms = await EventTermsRequests.ResolveAsync(
            request.TermsDocuments,
            termsDocuments,
            ct
        );
        if (terms.IsFailure)
        {
            return terms.Error!;
        }

        var previousThumbnailId = ev.ThumbnailId;
        var previousDescription = ev.Description;

        ev.Update(
            new EventContent(
                request.Title,
                request.Subtitle,
                request.Description,
                request.ThumbnailId
            ),
            schedule.Value,
            categories.Value,
            terms.Value,
            command.UserId,
            clock.UtcNow
        );

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

        return Result.Success();
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
