using System.Security.Cryptography;
using AwesomeAssertions;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication;

public sealed class EmailOutboxDelivererTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly EmailOutboxProtector protector = new(new EphemeralDataProtectionProvider());
    private readonly RecordingEmailSender transport = new();
    private readonly TestClock clock = new() { UtcNow = Now };
    private readonly EmailQueueOptions options = new();

    private static EmailBatch Batch(params string[] addresses)
    {
        return new EmailBatch(
            EmailKind.TwoFactorCode,
            "Tu código",
            "<p>482913</p>",
            "482913",
            [.. addresses.Select(address => new EmailRecipient(address, "Ana"))]
        );
    }

    private static EmailBatch BatchWithAttachment(params string[] addresses)
    {
        return new EmailBatch(
            EmailKind.Manual,
            "Asamblea general",
            "<p>Os esperamos</p>",
            "Os esperamos",
            [.. addresses.Select(address => new EmailRecipient(address, "Ana"))],
            [new EmailAttachment("acta.pdf", "application/pdf", "contenido"u8.ToArray())],
            [new EmailInlineImage("logo", "logo.png", "image/png", [137, 80, 78])]
        );
    }

    private FakeEmailOutboxStore Store()
    {
        return new FakeEmailOutboxStore(protector);
    }

    private EmailOutboxDeliverer Create(
        FakeEmailOutboxStore store,
        ILogger<EmailOutboxDeliverer>? logger = null
    )
    {
        return new EmailOutboxDeliverer(
            store,
            transport,
            protector,
            options,
            clock,
            logger ?? NullLogger<EmailOutboxDeliverer>.Instance
        );
    }

    [Fact]
    public async Task DeliverDueAsyncNothingStoredClaimsNothing()
    {
        var store = Store();

        var delivered = await Create(store).DeliverDueAsync(TestContext.Current.CancellationToken);

        delivered.Should().Be(0);
        transport.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task DeliverDueAsyncDeliveredMessageRemovesTheRowAndItsContent()
    {
        var store = Store();
        await store.TryEnqueueAsync(Batch("ana@example.test"), Ct());

        var delivered = await Create(store).DeliverDueAsync(Ct());

        delivered.Should().Be(1);
        transport.Sent.Should().ContainSingle().Which.TextBody.Should().Be("482913");
        store.Messages.Should().BeEmpty();
        store.Contents.Should().BeEmpty("the content of a delivered message is not kept");
    }

    [Fact]
    public async Task DeliverDueAsyncSharedContentSurvivesUntilItsLastMessageIsGone()
    {
        var store = Store();
        await store.TryEnqueueAsync(Batch("ana@example.test", "berto@example.test"), Ct());
        transport.FailingRecipients.Add("berto@example.test");

        await Create(store).DeliverDueAsync(Ct());

        store.Messages.Should().ContainSingle().Which.ToAddress.Should().Be("berto@example.test");
        store.Contents.Should().ContainSingle("a pending recipient still needs the stored content");

        transport.FailingRecipients.Clear();
        clock.UtcNow = Now.AddHours(1);
        await Create(store).DeliverDueAsync(Ct());

        store.Messages.Should().BeEmpty();
        store.Contents.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 2, 5)]
    [InlineData(2, 3, 30)]
    [InlineData(3, 4, 120)]
    public async Task DeliverDueAsyncFailedAttemptSchedulesTheNextOneWithAGrowingDelay(
        int alreadyFailed,
        int expectedAttempts,
        int expectedDelayMinutes
    )
    {
        var store = Store();
        await store.TryEnqueueAsync(Batch("ana@example.test"), Ct());
        store.SetAttemptCount(alreadyFailed);
        transport.ThrowOnSend = new InvalidOperationException("Connection refused");

        await Create(store).DeliverDueAsync(Ct());

        var retry = store.Rescheduled.Should().ContainSingle().Subject;
        retry.AttemptCount.Should().Be(expectedAttempts);
        retry.NextAttemptAt.Should().Be(Now.AddMinutes(expectedDelayMinutes));
        retry.Error.Should().Contain("Connection refused");
        store.Messages.Should().ContainSingle().Which.LockedUntil.Should().BeNull();
        store.Contents.Should().ContainSingle();
    }

    [Fact]
    public async Task DeliverDueAsyncLastAttemptFailsRemovesTheMessageAndLogsAnError()
    {
        var store = Store();
        await store.TryEnqueueAsync(Batch("ana@example.test"), Ct());
        store.SetAttemptCount(EmailQueueOptions.MaxAttempts - 1);
        transport.ThrowOnSend = new InvalidOperationException("Connection refused");
        var logger = new RecordingLogger<EmailOutboxDeliverer>();

        await Create(store, logger).DeliverDueAsync(Ct());

        store.Messages.Should().BeEmpty("the fifth failure discards the message");
        store.Contents.Should().BeEmpty();
        store.Rescheduled.Should().BeEmpty();
        var entry = logger
            .LevelEntries.Should()
            .ContainSingle(entry => entry.Level == LogLevel.Error)
            .Subject.Message;
        entry.Should().Contain("5").And.Contain("TwoFactorCode");
        entry.Should().NotContain("ana@example.test").And.NotContain("482913");
    }

    [Fact]
    public async Task DeliverDueAsyncSendTimesOutCountsAsAFailedAttempt()
    {
        var store = Store();
        await store.TryEnqueueAsync(Batch("ana@example.test"), Ct());
        options.SendTimeout = TimeSpan.FromMilliseconds(20);
        var blocking = new BlockingEmailTransport();
        var deliverer = new EmailOutboxDeliverer(
            store,
            blocking,
            protector,
            options,
            clock,
            NullLogger<EmailOutboxDeliverer>.Instance
        );

        await deliverer.DeliverDueAsync(Ct());

        var retry = store.Rescheduled.Should().ContainSingle().Subject;
        retry.AttemptCount.Should().Be(1);
        retry.NextAttemptAt.Should().Be(Now.AddMinutes(1));
        retry.Error.Should().Contain("Canceled");
    }

    [Fact]
    public async Task DeliverDueAsyncUnreadableContentCountsAsAFailedAttemptWithoutSending()
    {
        var store = new FakeEmailOutboxStore(
            new EmailOutboxProtector(new EphemeralDataProtectionProvider())
        );
        await store.TryEnqueueAsync(Batch("ana@example.test"), Ct());

        await Create(store).DeliverDueAsync(Ct());

        transport.Sent.Should().BeEmpty();
        var retry = store.Rescheduled.Should().ContainSingle().Subject;
        retry.AttemptCount.Should().Be(1);
        retry.Error.Should().Contain(nameof(CryptographicException));
    }

    [Fact]
    public async Task DeliverDueAsyncUnreadableSharedContentFailsEveryRecipientOfTheBatch()
    {
        var store = new FakeEmailOutboxStore(
            new EmailOutboxProtector(new EphemeralDataProtectionProvider())
        );
        await store.TryEnqueueAsync(
            BatchWithAttachment("uno@example.test", "dos@example.test", "tres@example.test"),
            Ct()
        );
        var provider = new CountingDataProtectionProvider();
        var deliverer = new EmailOutboxDeliverer(
            store,
            transport,
            new EmailOutboxProtector(provider),
            options,
            clock,
            NullLogger<EmailOutboxDeliverer>.Instance
        );

        var delivered = await deliverer.DeliverDueAsync(Ct());

        delivered.Should().Be(3);
        transport.Sent.Should().BeEmpty();
        store
            .Rescheduled.Should()
            .HaveCount(3, "every recipient of the batch spent its own attempt");
        store.Rescheduled.Select(retry => retry.Id).Should().OnlyHaveUniqueItems();
        store
            .Rescheduled.Should()
            .AllSatisfy(retry =>
            {
                retry.AttemptCount.Should().Be(1);
                retry.NextAttemptAt.Should().Be(Now.AddMinutes(1));
                retry.Error.Should().Contain(nameof(CryptographicException));
            });
        provider
            .Unprotections.Should()
            .Be(1, "the shared content is read once for the whole batch, and it fails once");
        store.Messages.Should().HaveCount(3);
        store.Contents.Should().ContainSingle();
    }

    [Fact]
    public async Task DeliverDueAsyncShutdownDuringTheClaimStillDeliversTheClaimedBatch()
    {
        var store = Store();
        await store.TryEnqueueAsync(Batch("ana@example.test", "berto@example.test"), Ct());
        using var shutdown = new CancellationTokenSource();
        store.OnClaim = shutdown.Cancel;

        var delivered = await Create(store).DeliverDueAsync(shutdown.Token);

        delivered.Should().Be(2);
        transport
            .Sent.Select(message => message.ToAddress)
            .Should()
            .BeEquivalentTo("ana@example.test", "berto@example.test");
        store.Messages.Should().BeEmpty("a claimed attempt is never dropped by a shutdown");
        store.Rescheduled.Should().BeEmpty();
    }

    [Fact]
    public async Task DeliverDueAsyncRecordingTheFailureFailsLeavesTheMessageToItsLease()
    {
        var store = new ThrowingOnWriteStore(protector);
        await store.TryEnqueueAsync(Batch("ana@example.test"), Ct());
        transport.ThrowOnSend = new InvalidOperationException("Connection refused");
        var logger = new RecordingLogger<EmailOutboxDeliverer>();

        var delivered = await Create(store, logger).DeliverDueAsync(Ct());

        delivered.Should().Be(1);
        logger
            .LevelEntries.Should()
            .Contain(entry =>
                entry.Level == LogLevel.Error && entry.Message.Contains("lease will expire")
            );
    }

    [Fact]
    public async Task DeliverDueAsyncClearingTheOrphanContentFailsIsLoggedWithoutFailingTheRun()
    {
        var store = new ThrowingOnSweepStore(protector);
        await store.TryEnqueueAsync(Batch("ana@example.test"), Ct());
        var logger = new RecordingLogger<EmailOutboxDeliverer>();

        var delivered = await Create(store, logger).DeliverDueAsync(Ct());

        delivered.Should().Be(1);
        transport.Sent.Should().ContainSingle();
        logger
            .LevelEntries.Should()
            .Contain(entry =>
                entry.Level == LogLevel.Error && entry.Message.Contains("stored content")
            );
    }

    [Fact]
    public async Task DeliverDueAsyncMoreMessagesThanTheBatchSizeClaimsOnlyOneBatch()
    {
        var store = Store();
        options.BatchSize = 2;
        await store.TryEnqueueAsync(
            Batch("uno@example.test", "dos@example.test", "tres@example.test"),
            Ct()
        );

        var delivered = await Create(store).DeliverDueAsync(Ct());

        delivered.Should().Be(2);
        store.Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task DeliverDueAsyncSharedContentIsUnprotectedOncePerBatch()
    {
        var provider = new CountingDataProtectionProvider();
        var shared = new EmailOutboxProtector(provider);
        var store = new FakeEmailOutboxStore(shared);
        await store.TryEnqueueAsync(
            BatchWithAttachment("uno@example.test", "dos@example.test", "tres@example.test"),
            Ct()
        );
        var deliverer = new EmailOutboxDeliverer(
            store,
            transport,
            shared,
            options,
            clock,
            NullLogger<EmailOutboxDeliverer>.Instance
        );

        await deliverer.DeliverDueAsync(Ct());

        transport.Sent.Should().HaveCount(3);
        provider
            .Unprotections.Should()
            .Be(
                5,
                "the subject, both bodies, the attachment and the inline image of the shared content are read once for the whole batch"
            );
        transport
            .Sent.Select(message => message.Attachments![0].Content)
            .Distinct()
            .Should()
            .ContainSingle("every recipient of the batch delivers the same bytes");
    }

    [Fact]
    public async Task DeliverDueAsyncFiveAbandonedClaimsRemoveTheMessageAndItsContent()
    {
        var store = Store();
        await store.TryEnqueueAsync(Batch("ana@example.test"), Ct());
        var logger = new RecordingLogger<EmailOutboxDeliverer>();

        for (var attempt = 1; attempt <= EmailQueueOptions.MaxAttempts; attempt++)
        {
            var claimed = await store.ClaimDueAsync(
                clock.UtcNow,
                clock.UtcNow + options.Lease,
                options.BatchSize,
                Ct()
            );

            claimed
                .Should()
                .ContainSingle("the process died before finishing the attempt")
                .Which.AttemptCount.Should()
                .Be(attempt, "the claim counts the attempt even when nothing finishes it");
            clock.UtcNow = clock.UtcNow + options.Lease + TimeSpan.FromSeconds(1);
        }

        var delivered = await Create(store, logger).DeliverDueAsync(Ct());

        delivered.Should().Be(0, "a message out of attempts is never claimed again");
        store.Messages.Should().BeEmpty("a message abandoned on its last attempt is discarded");
        store.Contents.Should().BeEmpty();
        transport.Sent.Should().BeEmpty();
        var entry = logger
            .LevelEntries.Should()
            .ContainSingle(entry => entry.Level == LogLevel.Error)
            .Subject.Message;
        entry.Should().Contain("5").And.Contain("TwoFactorCode");
        entry.Should().NotContain("ana@example.test").And.NotContain("482913");
    }

    [Fact]
    public async Task DeliverDueAsyncFailureAfterLosingTheLeaseLeavesTheReclaimedRowAlone()
    {
        var store = Store();
        await store.TryEnqueueAsync(Batch("ana@example.test"), Ct());
        var gate = new GatedFailingTransport();
        var lost = new EmailOutboxDeliverer(
            store,
            gate,
            protector,
            options,
            clock,
            NullLogger<EmailOutboxDeliverer>.Instance
        );
        transport.ThrowOnSend = new InvalidOperationException("Connection refused");

        var abandoned = lost.DeliverDueAsync(Ct());
        await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct());
        clock.UtcNow = clock.UtcNow + options.Lease + TimeSpan.FromSeconds(1);
        var reclaimedAt = clock.UtcNow;
        await Create(store).DeliverDueAsync(Ct());
        gate.Release();
        await abandoned;

        var pending = store.Messages.Should().ContainSingle().Subject;
        pending.AttemptCount.Should().Be(2);
        pending.NextAttemptAt.Should().Be(reclaimedAt + EmailQueueOptions.RetryDelayAfter(2));
        store
            .Rescheduled.Should()
            .ContainSingle("the worker that lost its lease never writes again")
            .Which.AttemptCount.Should()
            .Be(2);
    }

    [Fact]
    public async Task DeliverDueAsyncEmptyOutboxStillClearsOrphanContent()
    {
        var store = Store();
        await store.TryEnqueueAsync(Batch("ana@example.test"), Ct());
        var claimed = await store.ClaimDueAsync(
            clock.UtcNow,
            clock.UtcNow + options.Lease,
            options.BatchSize,
            Ct()
        );
        await store.RemoveAsync(claimed[0].Id, claimed[0].AttemptCount, Ct());
        store.Contents.Should().ContainSingle("the content outlived the message it belonged to");

        var delivered = await Create(store).DeliverDueAsync(Ct());

        delivered.Should().Be(0);
        store.Contents.Should().BeEmpty();
    }

    private static CancellationToken Ct()
    {
        return TestContext.Current.CancellationToken;
    }

    private sealed class GatedFailingTransport : IEmailTransport
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

        public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            Reached.TrySetResult();
            await gate.Task;
            throw new InvalidOperationException("The connection dropped after the lease expired.");
        }
    }

    private sealed class CountingDataProtectionProvider : IDataProtectionProvider
    {
        private readonly EphemeralDataProtectionProvider inner = new();
        private int unprotections;

        public int Unprotections => Volatile.Read(ref unprotections);

        public IDataProtector CreateProtector(string purpose)
        {
            return new CountingProtector(inner.CreateProtector(purpose), this);
        }

        private void Count()
        {
            Interlocked.Increment(ref unprotections);
        }

        private sealed class CountingProtector(
            IDataProtector inner,
            CountingDataProtectionProvider owner
        ) : IDataProtector
        {
            public IDataProtector CreateProtector(string purpose)
            {
                return inner.CreateProtector(purpose);
            }

            public byte[] Protect(byte[] plaintext)
            {
                return inner.Protect(plaintext);
            }

            public byte[] Unprotect(byte[] protectedData)
            {
                owner.Count();
                return inner.Unprotect(protectedData);
            }
        }
    }

    private sealed class BlockingEmailTransport : IEmailTransport
    {
        public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
    }

    private sealed class ThrowingOnWriteStore(EmailOutboxProtector protector)
        : FakeEmailOutboxStore(protector)
    {
        public override Task<int> RescheduleAsync(
            Guid messageId,
            int attemptCount,
            DateTimeOffset nextAttemptAt,
            string? error,
            CancellationToken ct = default
        )
        {
            throw new InvalidOperationException("The outbox write failed.");
        }
    }

    private sealed class ThrowingOnSweepStore(EmailOutboxProtector protector)
        : FakeEmailOutboxStore(protector)
    {
        public override Task<int> RemoveOrphanContentAsync(CancellationToken ct = default)
        {
            throw new InvalidOperationException("The content sweep failed.");
        }
    }
}
