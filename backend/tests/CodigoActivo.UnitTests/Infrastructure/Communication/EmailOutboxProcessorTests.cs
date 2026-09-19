using AwesomeAssertions;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication;

public sealed class EmailOutboxProcessorTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private readonly EmailOutboxProtector protector = new(new EphemeralDataProtectionProvider());
    private readonly EmailOutboxSignal signal = new();
    private readonly RecordingEmailSender transport = new();

    private static EmailBatch Batch(string address = "ana@example.test")
    {
        return new EmailBatch(
            EmailKind.TwoFactorCode,
            "Tu código",
            "<p>482913</p>",
            "482913",
            [new EmailRecipient(address, "Ana")]
        );
    }

    private EmailOutboxProcessor Create(
        FakeEmailOutboxStore store,
        EmailQueueOptions options,
        IEmailTransport? emailTransport = null,
        ILogger<EmailOutboxProcessor>? logger = null
    )
    {
        var deliverer = new EmailOutboxDeliverer(
            store,
            emailTransport ?? transport,
            protector,
            options,
            new TestClock { UtcNow = new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero) },
            NullLogger<EmailOutboxDeliverer>.Instance
        );

        return new EmailOutboxProcessor(
            deliverer,
            signal,
            options,
            logger ?? NullLogger<EmailOutboxProcessor>.Instance
        );
    }

    [Fact]
    public async Task ExecuteAsyncStoredMessageIsDeliveredAfterTheSignalWithoutWaitingForThePoll()
    {
        var store = new FakeEmailOutboxStore(protector);
        var options = new EmailQueueOptions { PollInterval = TimeSpan.FromMinutes(10) };
        var processor = Create(store, options);
        var delivered = new TaskCompletionSource();
        transport.OnSend = () => delivered.TrySetResult();

        await processor.StartAsync(TestContext.Current.CancellationToken);
        await store.TryEnqueueAsync(Batch(), TestContext.Current.CancellationToken);
        signal.Notify();

        await delivered.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);
        await processor.StopAsync(TestContext.Current.CancellationToken);

        transport.Sent.Should().ContainSingle();
        store.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsyncDeliveryRunThrowsLogsTheFailureAndKeepsRunning()
    {
        var store = new FakeEmailOutboxStore(protector)
        {
            ThrowOnClaim = new InvalidOperationException("The outbox is unreachable."),
        };
        var options = new EmailQueueOptions { PollInterval = TimeSpan.FromMilliseconds(5) };
        var logger = new RecordingLogger<EmailOutboxProcessor>();
        var processor = Create(store, options, logger: logger);
        var runs = 0;
        var twoRuns = new TaskCompletionSource();
        store.OnClaim = () =>
        {
            if (Interlocked.Increment(ref runs) >= 2)
            {
                twoRuns.TrySetResult();
            }
        };

        await processor.StartAsync(TestContext.Current.CancellationToken);
        await twoRuns.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);
        await processor.StopAsync(TestContext.Current.CancellationToken);

        logger
            .LevelEntries.Should()
            .Contain(entry =>
                entry.Level == LogLevel.Error && entry.Message.Contains("next run will retry")
            );
    }

    [Fact]
    public async Task StopAsyncDrainDeadlineElapsesReturnsAndWarnsWithoutTheMessageContent()
    {
        var store = new FakeEmailOutboxStore(protector);
        await store.TryEnqueueAsync(Batch(), TestContext.Current.CancellationToken);
        var options = new EmailQueueOptions
        {
            PollInterval = TimeSpan.FromMilliseconds(5),
            SendTimeout = TimeSpan.FromMinutes(5),
            ShutdownDrain = TimeSpan.FromMilliseconds(50),
        };
        var blocking = new BlockingEmailTransport();
        var logger = new RecordingLogger<EmailOutboxProcessor>();
        var processor = Create(store, options, blocking, logger);

        await processor.StartAsync(TestContext.Current.CancellationToken);
        await blocking.Reached.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);
        await processor.StopAsync(TestContext.Current.CancellationToken);

        logger
            .LevelEntries.Should()
            .Contain(entry =>
                entry.Level == LogLevel.Warning && entry.Message.Contains("stay stored")
            );
        logger.Entries.Should().NotContain(entry => entry.Contains("482913"));
        blocking.Release();
    }

    [Fact]
    public async Task ExecuteAsyncFullBatchKeepsDeliveringWithoutWaitingForTheNextPoll()
    {
        var store = new FakeEmailOutboxStore(protector);
        await store.TryEnqueueAsync(
            new EmailBatch(
                EmailKind.Manual,
                "Asunto",
                "<p>Hola</p>",
                "Hola",
                [
                    new EmailRecipient("uno@example.test", "Uno"),
                    new EmailRecipient("dos@example.test", "Dos"),
                    new EmailRecipient("tres@example.test", "Tres"),
                ]
            ),
            TestContext.Current.CancellationToken
        );
        var options = new EmailQueueOptions
        {
            BatchSize = 1,
            PollInterval = TimeSpan.FromMinutes(10),
        };
        var processor = Create(store, options);
        var emptied = new TaskCompletionSource();
        transport.OnSend = () =>
        {
            if (transport.Sent.Count == 3)
            {
                emptied.TrySetResult();
            }
        };

        await processor.StartAsync(TestContext.Current.CancellationToken);
        await emptied.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);
        await processor.StopAsync(TestContext.Current.CancellationToken);

        store.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsyncCancellationOutsideTheShutdownKeepsTheWorkerRunning()
    {
        var store = new FakeEmailOutboxStore(protector)
        {
            ThrowOnClaim = new OperationCanceledException("A database command timed out."),
        };
        var options = new EmailQueueOptions { PollInterval = TimeSpan.FromMilliseconds(5) };
        var logger = new RecordingLogger<EmailOutboxProcessor>();
        var processor = Create(store, options, logger: logger);
        var runs = 0;
        var twoRuns = new TaskCompletionSource();
        store.OnClaim = () =>
        {
            if (Interlocked.Increment(ref runs) >= 2)
            {
                twoRuns.TrySetResult();
            }
        };

        await processor.StartAsync(TestContext.Current.CancellationToken);
        await twoRuns.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);

        var delivered = new TaskCompletionSource();
        transport.OnSend = () => delivered.TrySetResult();
        store.OnClaim = null;
        store.ThrowOnClaim = null;
        await store.TryEnqueueAsync(Batch(), TestContext.Current.CancellationToken);

        await delivered.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);
        await processor.StopAsync(TestContext.Current.CancellationToken);

        transport.Sent.Should().ContainSingle("a foreign cancellation must not stop the worker");
        logger
            .LevelEntries.Should()
            .Contain(entry =>
                entry.Level == LogLevel.Error && entry.Message.Contains("next run will retry")
            );
    }

    [Fact]
    public async Task StopAsyncCancellationWhileClaimingEndsTheLoopWithoutLogging()
    {
        var store = new BlockingClaimStore(protector);
        var options = new EmailQueueOptions { PollInterval = TimeSpan.FromMilliseconds(5) };
        var logger = new RecordingLogger<EmailOutboxProcessor>();
        var processor = Create(store, options, logger: logger);

        await processor.StartAsync(TestContext.Current.CancellationToken);
        await store.Reached.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);
        await processor.StopAsync(TestContext.Current.CancellationToken);

        logger.Entries.Should().BeEmpty("a cancellation is the shutdown path, not a failure");
    }

    [Fact]
    public async Task StopAsyncWithoutWorkInFlightReturnsWithoutWarning()
    {
        var store = new FakeEmailOutboxStore(protector);
        var options = new EmailQueueOptions { PollInterval = TimeSpan.FromMilliseconds(5) };
        var logger = new RecordingLogger<EmailOutboxProcessor>();
        var processor = Create(store, options, logger: logger);

        await processor.StartAsync(TestContext.Current.CancellationToken);
        await processor.StopAsync(TestContext.Current.CancellationToken);

        logger.Entries.Should().BeEmpty();
    }

    private sealed class BlockingClaimStore(EmailOutboxProtector protector)
        : FakeEmailOutboxStore(protector)
    {
        public TaskCompletionSource Reached { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task<IReadOnlyList<EmailOutboxMessage>> ClaimDueAsync(
            DateTimeOffset now,
            DateTimeOffset leaseUntil,
            int batchSize,
            CancellationToken ct = default
        )
        {
            Reached.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return [];
        }
    }

    private sealed class BlockingEmailTransport : IEmailTransport
    {
        private readonly TaskCompletionSource gate = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        public TaskCompletionSource Reached { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release()
        {
            gate.TrySetResult();
        }

        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            Reached.TrySetResult();
            return gate.Task;
        }
    }
}
