using System.Collections.Concurrent;
using System.Reflection;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace CodigoActivo.Infrastructure.Messaging;

/// <summary>
/// Publishes committed domain events to the handlers registered in the current scope, in the
/// order the events were raised.
/// </summary>
/// <param name="services">Service provider of the current scope.</param>
public sealed class DomainEventPublisher(IServiceProvider services) : IDomainEventPublisher
{
    private static readonly MethodInfo PublishOneMethod = typeof(DomainEventPublisher).GetMethod(
        nameof(PublishOneAsync),
        BindingFlags.NonPublic | BindingFlags.Instance
    )!;

    private static readonly ConcurrentDictionary<Type, MethodInfo> PublishOneByEvent = new();

    /// <inheritdoc />
    public async Task PublishAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);
        if (domainEvents.Count is 0)
        {
            return;
        }

        foreach (var handler in services.GetServices<ICommittedEventsHandler>())
        {
            await handler.HandleAsync(domainEvents, ct);
        }

        foreach (var domainEvent in domainEvents)
        {
            var publishOne = PublishOneByEvent.GetOrAdd(
                domainEvent.GetType(),
                eventType => PublishOneMethod.MakeGenericMethod(eventType)
            );
            await (Task)publishOne.Invoke(this, [domainEvent, ct])!;
        }
    }

    private async Task PublishOneAsync<TEvent>(TEvent domainEvent, CancellationToken ct)
        where TEvent : IDomainEvent
    {
        foreach (var handler in services.GetServices<IDomainEventListener<TEvent>>())
        {
            await handler.HandleAsync(domainEvent, ct);
        }
    }
}
