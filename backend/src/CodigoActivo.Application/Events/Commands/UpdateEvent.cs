using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Common.Security;
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
/// <param name="Event">New content, schedule, categories and terms of the event.</param>
public sealed record UpdateEventCommand(EventId EventId, EventDraft Event) : ICommand<Result>;

/// <summary>
/// Executes the command to update the event.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="termsDocuments">Repository used to persist and retrieve terms documents.</param>
/// <param name="categoryChecker">The category checker value.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class UpdateEventCommandHandler(
    IEventRepository events,
    IActivityRepository activities,
    IStoredFileRepository files,
    ITermsDocumentRepository termsDocuments,
    EventCategoryChecker categoryChecker,
    ICurrentUser currentUser,
    IClock clock
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
        ArgumentNullException.ThrowIfNull(command);
        var draft = command.Event;

        var schedule = EventSchedule.Create(
            draft.EventStartsAt,
            draft.EventEndsAt,
            draft.EarlySignupStartsAt,
            draft.SignupStartsAt,
            draft.SignupEndsAt
        );
        if (schedule.IsFailure)
        {
            return schedule.Error!;
        }

        var categories = await categoryChecker.EnsureCategoriesAsync(draft.CategoryTypeIds, ct);
        if (categories.IsFailure)
        {
            return categories.Error!;
        }

        var ev = await events.GetByIdAsync(command.EventId, ct);
        if (ev is null)
        {
            return Error.NotFound(ApplicationErrorCode.EventNotFound);
        }

        var (lowerInclusive, upperExclusive) = DayBounds(
            schedule.Value.Calendar.Start,
            schedule.Value.Calendar.End
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
            return Error.Validation(ApplicationErrorCode.EventActivitiesOutsideNewRange);
        }

        if (!await files.ExistsAsync(draft.ThumbnailId, ct))
        {
            return Error.Validation(ApplicationErrorCode.EventThumbnailNotFound);
        }

        var terms = await EventTermsRequests.ResolveAsync(draft.TermsDocuments, termsDocuments, ct);
        if (terms.IsFailure)
        {
            return terms.Error!;
        }

        ev.Update(
            new EventContent(
                draft.Title,
                draft.Subtitle,
                RichText.From(draft.Description),
                draft.ThumbnailId
            ),
            schedule.Value,
            categories.Value,
            terms.Value,
            currentUser.RequiredId(),
            clock.UtcNow
        );
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
