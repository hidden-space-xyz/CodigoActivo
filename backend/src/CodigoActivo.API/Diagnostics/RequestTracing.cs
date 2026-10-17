using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CodigoActivo.API.Diagnostics;

/// <summary>
/// Gives every request a trace the API chose: an activity listener makes ASP.NET Core start an
/// activity for each request even when no other listener asks for it, and the propagator ignores
/// the <c>traceparent</c> and <c>tracestate</c> a client sends and adds none to outgoing calls.
/// The trace id is the reference error responses show and the one the diagnostic and audit logs
/// store, so a client can neither choose it nor learn it from elsewhere.
/// </summary>
internal static class RequestTracing
{
    /// <summary>
    /// Name of the activity source ASP.NET Core starts request activities from.
    /// </summary>
    internal const string HostingSource = "Microsoft.AspNetCore";

    /// <summary>
    /// Adds the request tracing to the host.
    /// </summary>
    /// <param name="services">Service collection of the host.</param>
    internal static void AddRequestTracing(this IServiceCollection services)
    {
        DistributedContextPropagator.Current = IsolatedTracePropagator.Instance;
        services.RemoveAll<DistributedContextPropagator>();
        services.AddSingleton<DistributedContextPropagator>(IsolatedTracePropagator.Instance);
        services.AddHostedService<RequestActivityListener>();
    }

    /// <summary>
    /// Creates the listener that makes the request activities exist without recording their
    /// data.
    /// </summary>
    /// <returns>A listener of <see cref="HostingSource"/> that asks for propagation data only.</returns>
    internal static ActivityListener CreateListener()
    {
        return new ActivityListener
        {
            ShouldListenTo = source => source.Name == HostingSource,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.PropagationData,
        };
    }

    /// <summary>
    /// Keeps the request activity listener registered while the host runs.
    /// </summary>
    private sealed class RequestActivityListener : IHostedService, IDisposable
    {
        private readonly ActivityListener listener = CreateListener();

        public Task StartAsync(CancellationToken cancellationToken)
        {
            ActivitySource.AddActivityListener(listener);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            listener.Dispose();
        }
    }
}

/// <summary>
/// Propagator that neither reads a trace from incoming requests nor writes one to outgoing ones.
/// </summary>
internal sealed class IsolatedTracePropagator : DistributedContextPropagator
{
    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    internal static IsolatedTracePropagator Instance { get; } = new();

    /// <inheritdoc />
    public override IReadOnlyCollection<string> Fields { get; } = [];

    /// <inheritdoc />
    public override void Inject(
        Activity? activity,
        object? carrier,
        PropagatorSetterCallback? setter
    ) { }

    /// <inheritdoc />
    public override void ExtractTraceIdAndState(
        object? carrier,
        PropagatorGetterCallback? getter,
        out string? traceId,
        out string? traceState
    )
    {
        traceId = null;
        traceState = null;
    }

    /// <inheritdoc />
    public override IEnumerable<KeyValuePair<string, string?>>? ExtractBaggage(
        object? carrier,
        PropagatorGetterCallback? getter
    )
    {
        return null;
    }
}
