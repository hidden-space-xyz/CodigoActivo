using System.Collections.Concurrent;
using System.Reflection;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Domain.Common;
using CodigoActivo.Infrastructure.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Messaging;

/// <summary>
/// Publishes committed domain events to the handlers registered in the current scope, in the
/// order the events were raised. Each handler runs without the cancellation of the request and
/// apart from the others: a failure is logged and the remaining handlers still run.
/// </summary>
/// <param name="services">Service provider of the current scope.</param>
/// <param name="logger">Logger of the effects that failed.</param>
public sealed class DomainEventPublisher(
    IServiceProvider services,
    ILogger<DomainEventPublisher> logger
) : IDomainEventPublisher
{
    private static readonly MethodInfo PublishOneMethod = typeof(DomainEventPublisher).GetMethod(
        nameof(PublishOneAsync),
        BindingFlags.NonPublic | BindingFlags.Instance
    )!;

    private static readonly ConcurrentDictionary<Type, MethodInfo> PublishOneByEvent = new();

    /// <inheritdoc />
    public async Task PublishAsync(IReadOnlyList<IDomainEvent> domainEvents)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);
        if (domainEvents.Count is 0)
        {
            return;
        }

        foreach (var handler in services.GetServices<ICommittedEventsHandler>())
        {
            await RunAsync(
                handler,
                () => handler.HandleAsync(domainEvents, CancellationToken.None)
            );
        }

        foreach (var domainEvent in domainEvents)
        {
            var publishOne = PublishOneByEvent.GetOrAdd(
                domainEvent.GetType(),
                eventType => PublishOneMethod.MakeGenericMethod(eventType)
            );
            await (Task)publishOne.Invoke(this, [domainEvent])!;
        }
    }

    private async Task PublishOneAsync<TEvent>(TEvent domainEvent)
        where TEvent : IDomainEvent
    {
        foreach (var handler in services.GetServices<IDomainEventListener<TEvent>>())
        {
            await RunAsync(handler, () => handler.HandleAsync(domainEvent, CancellationToken.None));
        }
    }

    private async Task RunAsync(object handler, Func<Task> effect)
    {
        try
        {
            await effect();
        }
        catch (Exception ex)
        {
            logger.CommittedEffectFailed(handler.GetType().Name, ex);
        }
    }
}
