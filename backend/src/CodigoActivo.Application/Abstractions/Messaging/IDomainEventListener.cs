using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Abstractions.Messaging;

/// <summary>
/// Reacts to a domain event once the change that raised it is committed.
/// </summary>
/// <typeparam name="TEvent">Type of event handled.</typeparam>
public interface IDomainEventListener<in TEvent>
    where TEvent : IDomainEvent
{
    /// <summary>
    /// Runs the effect of the event.
    /// </summary>
    /// <param name="domainEvent">Committed event.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task HandleAsync(TEvent domainEvent, CancellationToken ct);
}
