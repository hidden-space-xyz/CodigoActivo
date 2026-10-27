using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
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
/// <param name="Event">New content, schedule, categories and terms of the event.</param>
public sealed record UpdateEventCommand(EventId EventId, EventDraft Event) : ICommand<Result>;

/// <summary>
/// Executes the command to update the event. The event stays locked from the moment it is loaded
/// until its new state is stored, so a concurrent change of its categories, such as the deletion of
/// one of them, sees the stored categories and cannot leave the event without any. The new days
/// must still hold every activity of the event. A thumbnail or description image it no longer
/// references is removed after the commit when nothing else uses it.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="termsDocuments">Repository used to persist and retrieve terms documents.</param>
/// <param name="categoryChecker">Checker of the chosen categories.</param>
/// <param name="currentUser">Person the use case runs for, recorded as the editor.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work that stores the event while it is locked.</param>
public sealed class UpdateEventCommandHandler(
    IEventRepository events,
    IActivityRepository activities,
    IStoredFileRepository files,
    ITermsDocumentRepository termsDocuments,
    EventCategoryChecker categoryChecker,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork uow
) : ICommandHandler<UpdateEventCommand, Result>
{
    /// <summary>
    /// Handles the request to update the event.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result reports success; <see cref="ApplicationErrorCode.EventNotFound"/>;
    /// <see cref="ApplicationErrorCode.EventActivitiesOutsideNewRange"/> when an activity falls
    /// outside the new days; the domain error of an invalid schedule, category selection or terms
    /// list; <see cref="ApplicationErrorCode.EventCategoryTypeNotFound"/> or
    /// <see cref="ApplicationErrorCode.TermsDocumentNotFound"/> when a chosen one is missing, or
    /// <see cref="ApplicationErrorCode.EventThumbnailNotFound"/> or
    /// <see cref="ApplicationErrorCode.RichTextFileNotFound"/> when a file is missing.
    /// </returns>
    public async Task<Result> HandleAsync(
        UpdateEventCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        var draft = command.Event;

        var schedule = draft.ToSchedule();
        if (schedule.IsFailure)
        {
            return schedule.Error!;
        }

        var categories = await categoryChecker.EnsureCategoriesAsync(draft.CategoryTypeIds, ct);
        if (categories.IsFailure)
        {
            return categories.Error!;
        }

        return await uow.ExecuteInTransactionAsync(
            attempt => UpdateLockedAsync(command, schedule.Value, categories.Value, attempt),
            ct
        );
    }

    private async Task<Result> UpdateLockedAsync(
        UpdateEventCommand command,
        EventSchedule schedule,
        EventCategorySelection categories,
        CancellationToken ct
    )
    {
        var draft = command.Event;
        var ev = await events.GetLockedAsync(command.EventId, ct);
        if (ev is null)
        {
            return Error.NotFound(ApplicationErrorCode.EventNotFound);
        }

        var (lowerInclusive, upperExclusive) = schedule.Calendar.InstantsIn(clock.TimeZone);
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

        var description = RichText.From(draft.Description);
        var available = await ContentFiles.EnsureAvailableAsync(
            files,
            draft.ThumbnailId,
            description,
            ApplicationErrorCode.EventThumbnailNotFound,
            ct
        );
        if (available.IsFailure)
        {
            return available;
        }

        var terms = await EventTermsRequests.ResolveAsync(draft.TermsDocuments, termsDocuments, ct);
        if (terms.IsFailure)
        {
            return terms.Error!;
        }

        ev.Update(
            draft.ToContent(description),
            schedule,
            categories,
            terms.Value,
            currentUser.RequiredId(),
            clock.UtcNow
        );
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
