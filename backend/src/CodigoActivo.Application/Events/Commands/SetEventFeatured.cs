using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;

namespace CodigoActivo.Application.Events.Commands;

/// <summary>
/// Carries the input required to set event featured.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
public sealed record SetEventFeaturedCommand(Guid EventId) : ICommand<Result>;

/// <summary>
/// Executes the command to set event featured.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class SetEventFeaturedCommandHandler(
    IEventRepository events,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<SetEventFeaturedCommand, Result>
{
    /// <summary>
    /// Handles the request to set event featured.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or an application error on failure.</returns>
    public async Task<Result> HandleAsync(
        SetEventFeaturedCommand command,
        CancellationToken ct = default
    )
    {
        var chosen = await events.GetByIdAsync(command.EventId, ct);
        if (chosen is null)
        {
            return Error.NotFound(ErrorCode.EventNotFound);
        }

        FeaturedSelection.Choose(chosen, await events.ListFeaturedAsync(ct));
        await uow.SaveChangesAsync(ct);

        await cacheInvalidator.InvalidateAsync(CacheTags.Events);
        return Result.Success();
    }
}
