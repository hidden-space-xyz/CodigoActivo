using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Partners;
using CodigoActivo.Infrastructure.Messaging;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Messaging;

public sealed class DomainEventPublisherTests : IDisposable
{
    private static readonly PartnerCreated Raised = new(PartnerId.New());

    private readonly List<string> calls = [];
    private readonly List<CancellationToken> tokens = [];
    private readonly RecordingLogger<DomainEventPublisher> logger = new();
    private ServiceProvider? provider;

    public enum Failure
    {
        None,
        Faulted,
        ThrownBeforeAwait,
        Cancelled,
    }

    public void Dispose()
    {
        provider?.Dispose();
    }

    private DomainEventPublisher Build(params Effect[] effects)
    {
        var services = new ServiceCollection();
        foreach (var effect in effects)
        {
            services.AddSingleton<ICommittedEventsHandler>(new CommittedHandler(effect, this));
            services.AddSingleton<IDomainEventListener<PartnerCreated>>(new Listener(effect, this));
        }

        provider = services.BuildServiceProvider();
        return new DomainEventPublisher(provider, logger);
    }

    private Task RecordAsync(Effect effect, string kind, CancellationToken ct)
    {
        calls.Add($"{kind}:{effect.Name}");
        tokens.Add(ct);
        return effect.Failure switch
        {
            Failure.Faulted => FaultAsync(),
            Failure.ThrownBeforeAwait => throw new InvalidOperationException("Not reached."),
            Failure.Cancelled => Task.FromCanceled(new CancellationToken(canceled: true)),
            _ => Task.CompletedTask,
        };
    }

    private static async Task FaultAsync()
    {
        await Task.Yield();
        throw new InvalidOperationException("Not reached.");
    }

    [Fact]
    public async Task PublishAsyncSeveralEffectsRunCommittedHandlersBeforeListeners()
    {
        var publisher = Build(new Effect("first"), new Effect("second"));

        await publisher.PublishAsync([Raised]);

        calls.Should().Equal("commit:first", "commit:second", "event:first", "event:second");
        logger.Entries.Should().BeEmpty();
    }

    [Theory]
    [InlineData(Failure.Faulted)]
    [InlineData(Failure.ThrownBeforeAwait)]
    [InlineData(Failure.Cancelled)]
    public async Task PublishAsyncFailingEffectsLogAndLetTheOthersRun(Failure failure)
    {
        var publisher = Build(new Effect("broken", failure), new Effect("healthy"));

        var act = () => publisher.PublishAsync([Raised]);

        await act.Should().NotThrowAsync();
        calls.Should().Equal("commit:broken", "commit:healthy", "event:broken", "event:healthy");
        logger.LevelEntries.Should().OnlyContain(entry => entry.Level == LogLevel.Error);
        logger
            .Entries.Should()
            .SatisfyRespectively(
                entry => entry.Should().Contain(nameof(CommittedHandler)),
                entry => entry.Should().Contain(nameof(Listener))
            );
    }

    [Fact]
    public async Task PublishAsyncAnyEffectGetsATokenNobodyCanCancel()
    {
        var publisher = Build(new Effect("only"));

        await publisher.PublishAsync([Raised]);

        tokens.Should().HaveCount(2).And.OnlyContain(token => !token.CanBeCanceled);
    }

    [Fact]
    public async Task PublishAsyncNoEventsRunsNoEffect()
    {
        var publisher = Build(new Effect("only"));

        await publisher.PublishAsync([]);

        calls.Should().BeEmpty();
    }

    private sealed record Effect(string Name, Failure Failure = Failure.None);

    private sealed class CommittedHandler(Effect effect, DomainEventPublisherTests test)
        : ICommittedEventsHandler
    {
        public Task HandleAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken ct)
        {
            return test.RecordAsync(effect, "commit", ct);
        }
    }

    private sealed class Listener(Effect effect, DomainEventPublisherTests test)
        : IDomainEventListener<PartnerCreated>
    {
        public Task HandleAsync(PartnerCreated domainEvent, CancellationToken ct)
        {
            return test.RecordAsync(effect, "event", ct);
        }
    }
}
