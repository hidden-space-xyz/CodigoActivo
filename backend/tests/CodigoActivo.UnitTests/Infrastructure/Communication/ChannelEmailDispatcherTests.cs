using AwesomeAssertions;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication;

public sealed class ChannelEmailDispatcherTests
{
    private static readonly TimeSpan BlockingTimeout = TimeSpan.FromSeconds(30);

    private static EmailMessage Message(string address = "member@example.test")
    {
        return new EmailMessage(
            EmailKind.ActivityNotification,
            address,
            "Ana",
            "Asunto",
            "<p>Hola</p>",
            "Hola"
        );
    }

    private static ChannelEmailDispatcher Create(
        RecordingEmailSender transport,
        EmailQueueOptions? options = null
    )
    {
        return new ChannelEmailDispatcher(
            transport,
            options ?? new EmailQueueOptions(),
            NullLogger<ChannelEmailDispatcher>.Instance
        );
    }

    [Fact]
    public void TryEnqueue_QueueFull_ReturnsFalse()
    {
        var queue = Create(new RecordingEmailSender(), new EmailQueueOptions { Capacity = 1 });

        queue.TryEnqueue(Message()).Should().BeTrue();
        queue.TryEnqueue(Message()).Should().BeFalse();
    }

    [Fact]
    public async Task StopAsync_PendingMessages_DeliversThemBeforeReturning()
    {
        var transport = new RecordingEmailSender();
        var queue = Create(transport);

        queue.TryEnqueue(Message("uno@example.test"));
        queue.TryEnqueue(Message("dos@example.test"));

        await queue.StartAsync(TestContext.Current.CancellationToken);
        await queue.StopAsync(TestContext.Current.CancellationToken);

        transport
            .Sent.Select(m => m.ToAddress)
            .Should()
            .BeEquivalentTo("uno@example.test", "dos@example.test");
    }

    [Fact]
    public async Task StopAsync_TransportThrows_KeepsDeliveringTheRemainingMessages()
    {
        var transport = new RecordingEmailSender();
        transport.FailingRecipients.Add("roto@example.test");
        var queue = Create(transport);

        queue.TryEnqueue(Message("roto@example.test"));
        queue.TryEnqueue(Message("bueno@example.test"));

        await queue.StartAsync(TestContext.Current.CancellationToken);
        await queue.StopAsync(TestContext.Current.CancellationToken);

        transport.Sent.Select(m => m.ToAddress).Should().Equal("bueno@example.test");
    }

    [Fact]
    public async Task TryEnqueue_AfterTheQueueStopped_ReturnsFalse()
    {
        var transport = new RecordingEmailSender();
        var queue = Create(transport);

        await queue.StartAsync(TestContext.Current.CancellationToken);
        await queue.StopAsync(TestContext.Current.CancellationToken);

        queue.TryEnqueue(Message()).Should().BeFalse();
        transport.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task StopAsync_DrainDeadlineElapses_ReturnsWithoutWaitingForTheTransport()
    {
        var transport = new BlockingEmailTransport();
        var queue = new ChannelEmailDispatcher(
            transport,
            new EmailQueueOptions { ShutdownDrain = TimeSpan.FromMilliseconds(50) },
            NullLogger<ChannelEmailDispatcher>.Instance
        );

        queue.TryEnqueue(Message());
        await queue.StartAsync(TestContext.Current.CancellationToken);
        await transport.WaitUntilSendingAsync(
            BlockingTimeout,
            TestContext.Current.CancellationToken
        );

        await queue.StopAsync(TestContext.Current.CancellationToken);

        transport.Completed.Should().BeFalse();
        transport.Release();
    }

    [Fact]
    public async Task StartAsync_ConfiguredWorkers_DeliverConcurrently()
    {
        const int Workers = 3;
        var transport = new BlockingEmailTransport(Workers);
        var queue = new ChannelEmailDispatcher(
            transport,
            new EmailQueueOptions { Workers = Workers, ShutdownDrain = TimeSpan.FromSeconds(5) },
            NullLogger<ChannelEmailDispatcher>.Instance
        );

        queue.TryEnqueue(Message("uno@example.test"));
        queue.TryEnqueue(Message("dos@example.test"));
        queue.TryEnqueue(Message("tres@example.test"));

        await queue.StartAsync(TestContext.Current.CancellationToken);
        await transport.WaitUntilSendingAsync(
            BlockingTimeout,
            TestContext.Current.CancellationToken
        );

        transport.InFlight.Should().Be(Workers);

        transport.Release();
        await queue.StopAsync(TestContext.Current.CancellationToken);
    }

    private sealed class BlockingEmailTransport(int concurrency = 1) : IEmailTransport
    {
        private readonly TaskCompletionSource gate = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        private readonly TaskCompletionSource reached = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        private int inFlight;

        public bool Completed => gate.Task.IsCompleted;

        public int InFlight => Volatile.Read(ref inFlight);

        public Task WaitUntilSendingAsync(TimeSpan timeout, CancellationToken ct)
        {
            return reached.Task.WaitAsync(timeout, TimeProvider.System, ct);
        }

        public void Release()
        {
            gate.TrySetResult();
        }

        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            if (Interlocked.Increment(ref inFlight) >= concurrency)
            {
                reached.TrySetResult();
            }

            return gate.Task;
        }

        public Task<EmailBatchResult> SendManyAsync(
            IReadOnlyList<EmailMessage> messages,
            CancellationToken ct = default
        )
        {
            return Task.FromResult(new EmailBatchResult(0, 0));
        }
    }
}
