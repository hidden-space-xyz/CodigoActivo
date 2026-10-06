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
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task HandleAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken ct);
}
