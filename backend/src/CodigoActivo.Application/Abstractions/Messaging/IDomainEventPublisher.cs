using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Abstractions.Messaging;

/// <summary>
/// Hands committed domain events to their handlers.
/// </summary>
public interface IDomainEventPublisher
{
    /// <summary>
    /// Publishes the events of one commit: first to every <see cref="ICommittedEventsHandler"/>,
    /// then each event to its <see cref="IDomainEventListener{TEvent}"/>s.
    /// </summary>
    /// <param name="domainEvents">Events of one commit, in the order they were raised.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task PublishAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken ct);
}
