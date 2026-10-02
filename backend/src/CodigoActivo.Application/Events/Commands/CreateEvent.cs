using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Events.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.TermsDocuments;

namespace CodigoActivo.Application.Events.Commands;

/// <summary>
/// Carries the input required to create an event.
/// </summary>
/// <param name="Request">Validated client request data.</param>
/// <param name="UserId">Identifier of the user.</param>
public sealed record CreateEventCommand(CreateEventRequest Request, Guid UserId)
    : ICommand<Result<Guid>>;

/// <summary>
/// Executes the command to create an event.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="termsDocuments">Repository used to persist and retrieve terms documents.</param>
/// <param name="categoryChecker">The category checker value.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class CreateEventCommandHandler(
    IEventRepository events,
    IStoredFileRepository files,
    ITermsDocumentRepository termsDocuments,
    EventCategoryChecker categoryChecker,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<CreateEventCommand, Result<Guid>>
{
    /// <summary>
    /// Handles the request to create an event.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the identifier of the created item, or an application error on failure.</returns>
    public async Task<Result<Guid>> HandleAsync(
        CreateEventCommand command,
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

        if (!await files.ExistsAsync(request.ThumbnailId, ct))
        {
            return Error.Validation(ErrorCode.EventThumbnailNotFound);
        }

        var categories = await categoryChecker.EnsureCategoriesAsync(request.CategoryTypeIds, ct);
        if (categories.IsFailure)
        {
            return categories.Error!;
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

        var ev = Event.Create(
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

        await events.AddAsync(ev, ct);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Events);

        return ev.Id;
    }
}
