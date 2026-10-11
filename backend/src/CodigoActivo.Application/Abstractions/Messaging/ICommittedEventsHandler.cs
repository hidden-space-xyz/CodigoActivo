using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Abstractions.Messaging;

/// <summary>
/// Reacts once to every event of a commit together, before the handlers of each event run.
/// </summary>
public interface ICommittedEventsHandler
{
    /// <summary>
    /// Runs the effect of the committed events.
    /// </summary>
    /// <param name="domainEvents">Events of one commit, in the order they were raised.</param>
    /// <param name="ct">
    /// Token of the publisher, which never cancels it: the change is already committed, so the
    /// effect runs to its end even when the request that made it is aborted.
    /// </param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task HandleAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken ct);
}
