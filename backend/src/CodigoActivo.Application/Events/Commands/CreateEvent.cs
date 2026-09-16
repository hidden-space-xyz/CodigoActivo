using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Events.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Events.Commands;

/// <summary>
/// Carries the input required to create an event.
/// </summary>
/// <param name="Request">Validated client request data.</param>
/// <param name="UserId">Identifier of the user.</param>
public sealed record CreateEventCommand(CreateEventRequest Request, Guid UserId)
    : ICommand<Result<EventResponse>>;

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
/// <param name="getById">Handler used to retrieve event by identifier.</param>
public sealed class CreateEventCommandHandler(
    IEventRepository events,
    IFileRepository files,
    ITermsDocumentRepository termsDocuments,
    EventCategoryChecker categoryChecker,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator,
    GetEventByIdQueryHandler getById
) : ICommandHandler<CreateEventCommand, Result<EventResponse>>
{
    /// <summary>
    /// Handles the request to create an event.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an event on success, or an application error on failure.</returns>
    public async Task<Result<EventResponse>> HandleAsync(
        CreateEventCommand command,
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

        if (!await files.ExistsAsync(f => f.Id == request.ThumbnailId, ct))
        {
            return Error.BadRequest(ErrorCode.EventThumbnailNotFound);
        }

        var categories = await categoryChecker.EnsureCategoriesAsync(request.CategoryTypeIds, ct);
        if (categories.IsFailure)
        {
            return categories.Error!;
        }

        if (
            request.TermsDocumentId is { } termsDocumentId
            && !await termsDocuments.ExistsAsync(x => x.Id == termsDocumentId, ct)
        )
        {
            return Error.BadRequest(ErrorCode.TermsDocumentNotFound);
        }

        var ev = new Event
        {
            Title = request.Title.Trim(),
            Subtitle = request.Subtitle.Trim(),
            Description = request.Description,
            EventStartsAt = schedule.Value.EventStartsAt,
            EventEndsAt = schedule.Value.EventEndsAt,
            EarlySignupStartsAt = schedule.Value.EarlySignupStartsAt,
            SignupStartsAt = schedule.Value.SignupStartsAt,
            SignupEndsAt = schedule.Value.SignupEndsAt,
            ThumbnailId = request.ThumbnailId,
            TermsDocumentId = request.TermsDocumentId,
            CreatedAt = clock.UtcNow,
            CreatedBy = command.UserId,
        };
        EventRules.SyncCategories(ev, request.CategoryTypeIds!);

        await events.AddAsync(ev, ct);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Events);

        return await getById.HandleAsync(new GetEventByIdQuery(ev.Id), ct);
    }
}
