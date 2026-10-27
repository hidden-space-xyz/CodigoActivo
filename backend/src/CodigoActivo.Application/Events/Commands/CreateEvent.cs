using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.TermsDocuments;

namespace CodigoActivo.Application.Events.Commands;

/// <summary>
/// Carries the input required to create an event.
/// </summary>
/// <param name="Event">Content, schedule, categories and terms of the new event.</param>
public sealed record CreateEventCommand(EventDraft Event) : ICommand<Result<EventId>>;

/// <summary>
/// Executes the command to create an event.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="termsDocuments">Repository used to persist and retrieve terms documents.</param>
/// <param name="categoryChecker">Checker of the chosen categories.</param>
/// <param name="currentUser">Person the use case runs for, recorded as the creator.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work that saves the event in the transaction that locks its files.</param>
public sealed class CreateEventCommandHandler(
    IEventRepository events,
    IStoredFileRepository files,
    ITermsDocumentRepository termsDocuments,
    EventCategoryChecker categoryChecker,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork uow
) : ICommandHandler<CreateEventCommand, Result<EventId>>
{
    /// <summary>
    /// Handles the request to create an event.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the identifier of the new event; the domain error of an invalid
    /// schedule, category selection or terms list;
    /// <see cref="ApplicationErrorCode.EventCategoryTypeNotFound"/> or
    /// <see cref="ApplicationErrorCode.TermsDocumentNotFound"/> when a chosen one is missing, or
    /// <see cref="ApplicationErrorCode.EventThumbnailNotFound"/> or
    /// <see cref="ApplicationErrorCode.RichTextFileNotFound"/> when a file is missing.
    /// </returns>
    public async Task<Result<EventId>> HandleAsync(
        CreateEventCommand command,
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

        var terms = await EventTermsRequests.ResolveAsync(draft.TermsDocuments, termsDocuments, ct);
        if (terms.IsFailure)
        {
            return terms.Error!;
        }

        return await uow.ExecuteInTransactionAsync(
            attempt =>
                CreateLockedAsync(draft, schedule.Value, categories.Value, terms.Value, attempt),
            ct
        );
    }

    private async Task<Result<EventId>> CreateLockedAsync(
        EventDraft draft,
        EventSchedule schedule,
        EventCategorySelection categories,
        EventTermsLinks terms,
        CancellationToken ct
    )
    {
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
            return available.Error!;
        }

        var ev = Event.Create(
            draft.ToContent(description),
            schedule,
            categories,
            terms,
            currentUser.RequiredId(),
            clock.UtcNow
        );

        await events.AddAsync(ev, ct);
        await uow.SaveChangesAsync(ct);
        return ev.Id;
    }
}
