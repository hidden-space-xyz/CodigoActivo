using System.Diagnostics;
using AwesomeAssertions;
using CodigoActivo.API.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CodigoActivo.UnitTests.API.Diagnostics;

public sealed class RequestTracingTests
{
    private const string IncomingTrace = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

    [Fact]
    public void PropagatorIgnoresTheTraceAndBaggageAClientSends()
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["traceparent"] = IncomingTrace,
            ["tracestate"] = "vendor=value",
            ["baggage"] = "user=ana",
        };

        IsolatedTracePropagator.Instance.ExtractTraceIdAndState(
            headers,
            static (carrier, name, out value, out values) =>
            {
                values = null;
                value = ((Dictionary<string, string>)carrier!).GetValueOrDefault(name);
            },
            out var traceId,
            out var traceState
        );

        traceId.Should().BeNull();
        traceState.Should().BeNull();
        IsolatedTracePropagator
            .Instance.ExtractBaggage(
                headers,
                static (carrier, name, out value, out values) =>
                {
                    values = null;
                    value = ((Dictionary<string, string>)carrier!).GetValueOrDefault(name);
                }
            )
            .Should()
            .BeNull();
    }

    [Fact]
    public void PropagatorWritesNothingToOutgoingCalls()
    {
        using var activity = new Activity("outgoing").Start();
        var written = new List<string>();

        IsolatedTracePropagator.Instance.Inject(
            activity,
            written,
            static (carrier, name, _) => ((List<string>)carrier!).Add(name)
        );

        written.Should().BeEmpty();
        IsolatedTracePropagator.Instance.Fields.Should().BeEmpty();
    }

    [Fact]
    public void ListenerMakesRequestActivitiesExistWithoutRecordingTheirData()
    {
        using var listener = RequestTracing.CreateListener();
        ActivitySource.AddActivityListener(listener);
        using var hosting = new ActivitySource(RequestTracing.HostingSource);
        using var other = new ActivitySource("CodigoActivo.Tests.Unlistened");

        using var request = hosting.StartActivity("request", ActivityKind.Server);
        using var unrelated = other.StartActivity("unrelated");

        request.Should().NotBeNull();
        request!.TraceId.ToHexString().Should().MatchRegex("^[0-9a-f]{32}$");
        request.IsAllDataRequested.Should().BeFalse();
        unrelated.Should().BeNull();
    }

    [Fact]
    public void AddRequestTracingReplacesThePropagatorAndKeepsTheListenerRunning()
    {
        var services = new ServiceCollection();
        services.AddSingleton(DistributedContextPropagator.CreateDefaultPropagator());

        services.AddRequestTracing();

        using var provider = services.BuildServiceProvider();
        provider
            .GetRequiredService<DistributedContextPropagator>()
            .Should()
            .BeSameAs(IsolatedTracePropagator.Instance);
        DistributedContextPropagator.Current.Should().BeSameAs(IsolatedTracePropagator.Instance);
        provider.GetServices<IHostedService>().Should().ContainSingle();
    }
}
