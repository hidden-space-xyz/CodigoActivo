using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Abstractions.Messaging;

/// <summary>
/// Hands committed domain events to their handlers.
/// </summary>
public interface IDomainEventPublisher
{
    /// <summary>
    /// Publishes the events of one commit: first to every <see cref="ICommittedEventsHandler"/>,
    /// then each event to its <see cref="IDomainEventListener{TEvent}"/>s. The change is already
    /// committed, so every effect runs to its end even when the request that made the change is
    /// aborted, and a failing effect neither stops the others nor fails the caller.
    /// </summary>
    /// <param name="domainEvents">Events of one commit, in the order they were raised.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task PublishAsync(IReadOnlyList<IDomainEvent> domainEvents);
}
