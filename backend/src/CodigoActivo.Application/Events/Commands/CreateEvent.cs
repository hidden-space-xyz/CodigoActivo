using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
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
/// <param name="categoryChecker">The category checker value.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class CreateEventCommandHandler(
    IEventRepository events,
    IStoredFileRepository files,
    ITermsDocumentRepository termsDocuments,
    EventCategoryChecker categoryChecker,
    ICurrentUser currentUser,
    IClock clock
) : ICommandHandler<CreateEventCommand, Result<EventId>>
{
    /// <summary>
    /// Handles the request to create an event.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the identifier of the created item, or an application error on failure.</returns>
    public async Task<Result<EventId>> HandleAsync(
        CreateEventCommand command,
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

        if (!await files.ExistsAsync(draft.ThumbnailId, ct))
        {
            return Error.Validation(ApplicationErrorCode.EventThumbnailNotFound);
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

        var ev = Event.Create(
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

        await events.AddAsync(ev, ct);
        return ev.Id;
    }
}
