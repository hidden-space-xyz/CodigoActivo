using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Domain.Activities;

namespace CodigoActivo.Application.Activities;

/// <summary>
/// Tells a person, or their guardian, that their signup was confirmed or denied, once the
/// decision is committed.
/// </summary>
/// <param name="notifier">Sender of the signup notifications.</param>
public sealed class AssignmentDecisionNotification(ActivitySignupNotifier notifier)
    : IDomainEventListener<AssignmentStatusChanged>
{
    /// <inheritdoc />
    public async Task HandleAsync(AssignmentStatusChanged domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (!AssignmentDecisions.IsDecision(domainEvent.Status))
        {
            return;
        }

        await notifier.NotifyDecisionAsync(
            domainEvent.ActivityId.Value,
            domainEvent.UserId.Value,
            domainEvent.Status,
            domainEvent.Role,
            ct
        );
    }
}
