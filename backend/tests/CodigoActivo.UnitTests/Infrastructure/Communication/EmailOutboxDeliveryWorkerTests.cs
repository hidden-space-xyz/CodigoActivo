using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication;

public sealed class EmailOutboxDeliveryWorkerTests : IAsyncDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private readonly EmailOutboxProtector protector = new(new EphemeralDataProtectionProvider());
    private readonly EmailOutboxSignal signal = new();
    private readonly RecordingEmailSender transport = new();
    private readonly EmailGuardOptions guard = new();
    private readonly FakeDeliveryLock deliveryLock = new();
    private readonly TestClock clock = new()
    {
        UtcNow = new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero),
    };

    public ValueTask DisposeAsync()
    {
        return deliveryLock.DisposeAsync();
    }

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

    private EmailOutboxDeliveryWorker Create(
        FakeEmailOutboxStore store,
        EmailQueueOptions options,
        IEmailTransport? emailTransport = null,
        ILogger<EmailOutboxDeliveryWorker>? logger = null
    )
    {
        var smtp = new SmtpOptions();
        var deliverer = new EmailOutboxDeliverer(
            store,
            emailTransport ?? transport,
            protector,
            new EmailDeliveryPacer(smtp, guard, clock),
            options,
            smtp,
            guard,
            clock,
            NullLogger<EmailOutboxDeliverer>.Instance
        );

        return new EmailOutboxDeliveryWorker(
            deliverer,
            deliveryLock,
            signal,
            options,
            guard,
            clock,
            logger ?? NullLogger<EmailOutboxDeliveryWorker>.Instance
        );
    }

    [Fact]
    public async Task ExecuteAsyncStoredMessageIsDeliveredAfterTheSignalWithoutWaitingForThePoll()
    {
        var store = new FakeEmailOutboxStore(protector) { EnqueuedAt = clock.UtcNow };
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
    public async Task ExecuteAsyncDeliveryRunKeepsThrowingLogsTheFailureOnceAndKeepsRunning()
    {
        var store = new FakeEmailOutboxStore(protector)
        {
            EnqueuedAt = clock.UtcNow,
            ThrowOnClaim = new InvalidOperationException("The outbox is unreachable."),
        };
        var options = new EmailQueueOptions { PollInterval = TimeSpan.FromMilliseconds(5) };
        var logger = new RecordingLogger<EmailOutboxDeliveryWorker>();
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
            .ContainSingle()
            .Which.Should()
            .Match<(LogLevel Level, string Message)>(entry =>
                entry.Level == LogLevel.Error && entry.Message.Contains("next run will retry")
            );
    }

    [Fact]
    public async Task StopAsyncDrainDeadlineElapsesReturnsWithoutLogging()
    {
        var store = new FakeEmailOutboxStore(protector) { EnqueuedAt = clock.UtcNow };
        await store.TryEnqueueAsync(Batch(), TestContext.Current.CancellationToken);
        var options = new EmailQueueOptions
        {
            PollInterval = TimeSpan.FromMilliseconds(5),
            SendTimeout = TimeSpan.FromMinutes(5),
            ShutdownDrain = TimeSpan.FromMilliseconds(50),
        };
        var blocking = new BlockingEmailTransport();
        var logger = new RecordingLogger<EmailOutboxDeliveryWorker>();
        var processor = Create(store, options, blocking, logger);

        await processor.StartAsync(TestContext.Current.CancellationToken);
        await blocking.Reached.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);
        await processor.StopAsync(TestContext.Current.CancellationToken);

        logger.Entries.Should().BeEmpty("the messages in flight stay stored for the next start");
        blocking.Release();
    }

    [Fact]
    public async Task ExecuteAsyncFullBatchKeepsDeliveringWithoutWaitingForTheNextPoll()
    {
        var store = new FakeEmailOutboxStore(protector) { EnqueuedAt = clock.UtcNow };
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
            EnqueuedAt = clock.UtcNow,
            ThrowOnClaim = new OperationCanceledException("A database command timed out."),
        };
        var options = new EmailQueueOptions { PollInterval = TimeSpan.FromMilliseconds(5) };
        var logger = new RecordingLogger<EmailOutboxDeliveryWorker>();
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
        var logger = new RecordingLogger<EmailOutboxDeliveryWorker>();
        var processor = Create(store, options, logger: logger);

        await processor.StartAsync(TestContext.Current.CancellationToken);
        await store.Reached.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);
        await processor.StopAsync(TestContext.Current.CancellationToken);

        logger.Entries.Should().BeEmpty("a cancellation is the shutdown path, not a failure");
    }

    [Fact]
    public async Task ExecuteAsyncAnotherInstanceHoldsTheLockDeliversNothing()
    {
        var store = new FakeEmailOutboxStore(protector) { EnqueuedAt = clock.UtcNow };
        await store.TryEnqueueAsync(Batch(), TestContext.Current.CancellationToken);
        var options = new EmailQueueOptions { PollInterval = TimeSpan.FromMilliseconds(5) };
        deliveryLock.Held = false;
        var processor = Create(store, options);

        await processor.StartAsync(TestContext.Current.CancellationToken);
        await deliveryLock.Asked.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);
        await processor.StopAsync(TestContext.Current.CancellationToken);

        transport.Sent.Should().BeEmpty("only the instance that holds the lock delivers");
        store.Messages.Should().ContainSingle();
        deliveryLock.Disposed.Should().BeTrue("stopping releases the lock");
    }

    [Fact]
    public async Task StopAsyncWithoutWorkInFlightReturnsWithoutWarning()
    {
        var store = new FakeEmailOutboxStore(protector) { EnqueuedAt = clock.UtcNow };
        var options = new EmailQueueOptions { PollInterval = TimeSpan.FromMilliseconds(5) };
        var logger = new RecordingLogger<EmailOutboxDeliveryWorker>();
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
            int maxPriority,
            CancellationToken ct = default
        )
        {
            Reached.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return [];
        }
    }

    private sealed class FakeDeliveryLock : IEmailDeliveryLock
    {
        public bool Held { get; set; } = true;

        public bool Disposed { get; private set; }

        public TaskCompletionSource Asked { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<bool> HoldAsync(CancellationToken ct)
        {
            Asked.TrySetResult();
            return Task.FromResult(Held);
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class BlockingEmailTransport : IEmailTransport, IEmailTransportSession
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

        public Task<IEmailTransportSession> OpenAsync(CancellationToken ct = default)
        {
            return Task.FromResult<IEmailTransportSession>(this);
        }

        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            Reached.TrySetResult();
            return gate.Task;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
