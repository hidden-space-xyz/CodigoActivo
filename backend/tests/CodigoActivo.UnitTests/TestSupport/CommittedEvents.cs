using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Domain.Common;
using Xunit;

namespace CodigoActivo.UnitTests.TestSupport;

internal sealed class CommittedEvents(params object[] listeners)
{
    public async Task PublishAsync(params IHasDomainEvents[] aggregates)
    {
        foreach (
            var domainEvent in aggregates.SelectMany(aggregate => aggregate.PullDomainEvents())
        )
        {
            var contract = typeof(IDomainEventListener<>).MakeGenericType(domainEvent.GetType());
            var handle = contract.GetMethod(
                nameof(IDomainEventListener<IDomainEvent>.HandleAsync)
            )!;
            foreach (var listener in listeners.Where(contract.IsInstanceOfType))
            {
                await (Task)
                    handle.Invoke(listener, [domainEvent, TestContext.Current.CancellationToken])!;
            }
        }
    }
}
