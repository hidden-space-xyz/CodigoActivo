using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
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
    private readonly SmtpOptions smtp = new();
    private readonly EmailGuardOptions guard = new();

    private static EmailBatch Batch(params string[] addresses)
    {
        return Batch(EmailKind.TwoFactorCode, addresses);
    }

    private static EmailBatch Batch(EmailKind kind, params string[] addresses)
    {
        return new EmailBatch(
            kind,
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
        return new FakeEmailOutboxStore(protector) { EnqueuedAt = Now };
    }

    private EmailOutboxDeliverer Create(
        FakeEmailOutboxStore store,
        ILogger<EmailOutboxDeliverer>? logger = null,
        IEmailTransport? with = null,
        EmailOutboxProtector? reader = null
    )
    {
        return new EmailOutboxDeliverer(
            store,
            with ?? transport,
            reader ?? protector,
            new EmailDeliveryPacer(smtp, guard, clock),
            options,
            smtp,
            guard,
            clock,
            logger ?? NullLogger<EmailOutboxDeliverer>.Instance
        );
    }

    private static EmailDeliveryException ServerDown()
    {
        return new EmailDeliveryException(
            EmailDeliveryFailure.ServerUnavailable,
            "Could not open a session with the SMTP server: SocketException",
            null
        );
    }

    [Fact]
    public async Task DeliverDueAsyncNothingStoredClaimsNothingAndOpensNoSession()
    {
        var store = Store();

        var delivered = await Create(store).DeliverDueAsync(Ct());

        delivered.Should().Be(0);
        transport.Sent.Should().BeEmpty();
        transport.Opened.Should().Be(0);
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
    public async Task DeliverDueAsyncWholeBatchSharesOneSessionAndIsCounted()
    {
        var store = Store();
        await store.TryEnqueueAsync(
            Batch("uno@example.test", "dos@example.test", "tres@example.test"),
            Ct()
        );

        await Create(store).DeliverDueAsync(Ct());

        transport.Sent.Should().HaveCount(3);
        transport.Opened.Should().Be(1, "every message of a run goes over one connection");
        store.SentRecorded.Should().Be(3);
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
    [InlineData(3, 4, 60)]
    [InlineData(10, 11, 60)]
    public async Task DeliverDueAsyncFailedAttemptSchedulesTheNextOneWithAGrowingDelay(
        int alreadyFailed,
        int expectedAttempts,
        int expectedDelayMinutes
    )
    {
        var store = Store();
        await store.TryEnqueueAsync(Batch("ana@example.test"), Ct());
        store.SetAttemptCount(alreadyFailed);
        transport.ThrowOnSend = new InvalidOperationException("Connection reset");

        await Create(store).DeliverDueAsync(Ct());

        var retry = store.Rescheduled.Should().ContainSingle().Subject;
        retry.AttemptCount.Should().Be(expectedAttempts);
        retry.NextAttemptAt.Should().Be(Now.AddMinutes(expectedDelayMinutes));
        retry.Error.Should().Contain("Connection reset");
        store.Messages.Should().ContainSingle().Which.LockedUntil.Should().BeNull();
    }

    [Fact]
    public async Task DeliverDueAsyncServerUnavailableHandsTheBatchBackWithoutSpendingAttempts()
    {
        var store = Store();
        await store.TryEnqueueAsync(Batch("uno@example.test", "dos@example.test"), Ct());
        transport.ThrowOnOpen = ServerDown();
        var logger = new RecordingLogger<EmailOutboxDeliverer>();
        var deliverer = Create(store, logger);

        await deliverer.DeliverDueAsync(Ct());

        deliverer.PausedUntil.Should().Be(Now.AddMinutes(1));
        store.Rescheduled.Should().BeEmpty();
        store
            .Messages.Should()
            .HaveCount(2)
            .And.AllSatisfy(message =>
            {
                message.AttemptCount.Should().Be(0, "a pause spends no attempt");
                message.NextAttemptAt.Should().Be(Now.AddMinutes(1));
                message.LockedUntil.Should().BeNull();
            });
        logger.LevelEntries.Should().ContainSingle(entry => entry.Level == LogLevel.Error);
        store.SentRecorded.Should().Be(0);
    }

    [Fact]
    public async Task DeliverDueAsyncWhilePausedClaimsNothing()
    {
        var store = Store();
        await store.TryEnqueueAsync(Batch("ana@example.test"), Ct());
        transport.ThrowOnOpen = ServerDown();
        var deliverer = Create(store);
        await deliverer.DeliverDueAsync(Ct());
        transport.ThrowOnOpen = null;

        clock.UtcNow = Now.AddSeconds(30);
        (await deliverer.DeliverDueAsync(Ct())).Should().Be(0);

        clock.UtcNow = Now.AddMinutes(1);
        (await deliverer.DeliverDueAsync(Ct())).Should().Be(1);
        transport.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task DeliverDueAsyncServerDownAgainPausesLongerUntilASendSucceeds()
    {
        var store = Store();
        await store.TryEnqueueAsync(Batch("ana@example.test"), Ct());
        transport.ThrowOnOpen = ServerDown();
        var deliverer = Create(store);

        await deliverer.DeliverDueAsync(Ct());
        clock.UtcNow = deliverer.PausedUntil;
        await deliverer.DeliverDueAsync(Ct());

        deliverer.PausedUntil.Should().Be(clock.UtcNow.AddMinutes(2));

        transport.ThrowOnOpen = null;
        clock.UtcNow = deliverer.PausedUntil;
        await deliverer.DeliverDueAsync(Ct());
        await store.TryEnqueueAsync(Batch("berto@example.test"), Ct());
        transport.ThrowOnOpen = ServerDown();
        await deliverer.DeliverDueAsync(Ct());

        deliverer
            .PausedUntil.Should()
            .Be(clock.UtcNow.AddMinutes(1), "a success resets the pauses");
    }

    [Fact]
    public async Task DeliverDueAsyncThreeFailuresInARowAreBlamedOnTheServer()
    {
        var store = Store();
        await store.TryEnqueueAsync(
            Batch(
                "uno@example.test",
                "dos@example.test",
                "tres@example.test",
                "cuatro@example.test"
            ),
            Ct()
        );
        transport.ThrowOnSend = new InvalidOperationException("Connection reset");
        var deliverer = Create(store);

        await deliverer.DeliverDueAsync(Ct());

        store.Rescheduled.Should().HaveCount(3);
        store.Released.Should().ContainSingle("the fourth message was never tried");
        deliverer.PausedUntil.Should().Be(Now.AddMinutes(1));
        store.SentRecorded.Should().Be(3);
    }

    [Fact]
    public async Task DeliverDueAsyncDeferredRecipientsAreRetriedWithoutPausingTheDelivery()
    {
        var store = Store();
        string[] deferred = ["uno@example.test", "dos@example.test", "tres@example.test"];
        await store.TryEnqueueAsync(Batch([.. deferred, "cuatro@example.test"]), Ct());
        transport.FailingRecipients.UnionWith(deferred);
        var deliverer = Create(store);

        await deliverer.DeliverDueAsync(Ct());

        store.Rescheduled.Should().HaveCount(3);
        transport.Sent.Should().ContainSingle().Which.ToAddress.Should().Be("cuatro@example.test");
        deliverer
            .PausedUntil.Should()
            .BeBefore(Now, "a greylisted recipient is not a server outage");
    }

    [Fact]
    public async Task DeliverDueAsyncRecipientRejectedDropsTheMessageAndWarnsWithoutTheAddress()
    {
        var store = Store();
        await store.TryEnqueueAsync(Batch("nadie@example.test", "ana@example.test"), Ct());
        transport.RejectedRecipients.Add("nadie@example.test");
        var logger = new RecordingLogger<EmailOutboxDeliverer>();

        await Create(store, logger).DeliverDueAsync(Ct());

        store.Messages.Should().BeEmpty();
        store.Rescheduled.Should().BeEmpty("a refusal for good is never retried");
        transport.Sent.Should().ContainSingle().Which.ToAddress.Should().Be("ana@example.test");
        var entry = logger
            .LevelEntries.Should()
            .ContainSingle(entry => entry.Level == LogLevel.Warning)
            .Subject.Message;
        entry.Should().Contain("RecipientRejected").And.Contain("TwoFactorCode");
        entry.Should().NotContain("nadie@example.test");
    }

    [Fact]
    public async Task DeliverDueAsyncUnreadableSharedContentDropsEveryRecipientWithoutSending()
    {
        var store = new FakeEmailOutboxStore(
            new EmailOutboxProtector(new EphemeralDataProtectionProvider())
        )
        {
            EnqueuedAt = Now,
        };
        await store.TryEnqueueAsync(
            BatchWithAttachment("uno@example.test", "dos@example.test", "tres@example.test"),
            Ct()
        );
        var provider = new CountingDataProtectionProvider();
        var logger = new RecordingLogger<EmailOutboxDeliverer>();

        var delivered = await Create(store, logger, reader: new EmailOutboxProtector(provider))
            .DeliverDueAsync(Ct());

        delivered.Should().Be(3);
        transport.Sent.Should().BeEmpty();
        transport.Opened.Should().Be(0);
        store.Messages.Should().BeEmpty("content no key can read is never worth retrying");
        provider.Unprotections.Should().Be(1, "the shared content is read once and fails once");
        logger
            .LevelEntries.Should()
            .HaveCount(3)
            .And.AllSatisfy(entry => entry.Message.Should().Contain("MessageRejected"));
    }

    [Fact]
    public async Task DeliverDueAsyncSendTimesOutCountsAsAFailedAttempt()
    {
        var store = Store();
        await store.TryEnqueueAsync(Batch("ana@example.test"), Ct());
        options.SendTimeout = TimeSpan.FromMilliseconds(20);

        await Create(store, with: new BlockingEmailTransport()).DeliverDueAsync(Ct());

        var retry = store.Rescheduled.Should().ContainSingle().Subject;
        retry.AttemptCount.Should().Be(1);
        retry.NextAttemptAt.Should().Be(Now.AddMinutes(1));
        retry.Error.Should().Contain("Canceled");
    }

    [Fact]
    public async Task DeliverDueAsyncExpiredMessageIsRemovedAndLoggedWithoutTheAddress()
    {
        var store = Store();
        await store.TryEnqueueAsync(
            Batch("ana@example.test") with
            {
                ValidFor = TimeSpan.FromMinutes(10),
            },
            Ct()
        );
        var logger = new RecordingLogger<EmailOutboxDeliverer>();
        clock.UtcNow = Now.AddMinutes(10);

        var delivered = await Create(store, logger).DeliverDueAsync(Ct());

        delivered.Should().Be(0);
        transport.Sent.Should().BeEmpty("a code is useless once it expired");
        store.Messages.Should().BeEmpty();
        var entry = logger
            .LevelEntries.Should()
            .ContainSingle(entry => entry.Level == LogLevel.Error)
            .Subject.Message;
        entry.Should().Contain("1").And.Contain("TwoFactorCode").And.NotContain("ana@example.test");
    }

    [Fact]
    public async Task DeliverDueAsyncNoticeThatNeverExpiresIsReportedOnceOverdue()
    {
        var store = Store();
        store.EnqueuedAt = Now.AddHours(-25);
        await store.TryEnqueueAsync(
            Batch(EmailKind.ConsentWithdrawalNotice, "legal@example.test"),
            Ct()
        );
        transport.ThrowOnOpen = ServerDown();
        var logger = new RecordingLogger<EmailOutboxDeliverer>();
        var deliverer = Create(store, logger);

        await deliverer.DeliverDueAsync(Ct());
        clock.UtcNow = Now.AddMinutes(1);
        await deliverer.DeliverDueAsync(Ct());

        store.Messages.Should().ContainSingle("a notice to the legal contact never expires");
        logger
            .Entries.Should()
            .ContainSingle(
                entry => entry.Contains("more than 24 hours"),
                "the check runs once per alert interval"
            );
    }

    [Fact]
    public async Task DeliverDueAsyncProviderPacePerMinuteLimitsTheClaim()
    {
        var store = Store();
        smtp.MaxPerMinute = 2;
        await store.TryEnqueueAsync(
            Batch("uno@example.test", "dos@example.test", "tres@example.test"),
            Ct()
        );
        var deliverer = Create(store);

        (await deliverer.DeliverDueAsync(Ct())).Should().Be(2);
        (await deliverer.DeliverDueAsync(Ct())).Should().Be(0, "the minute budget is spent");

        clock.UtcNow = Now.AddSeconds(30);
        (await deliverer.DeliverDueAsync(Ct())).Should().Be(1);
        transport.Sent.Should().HaveCount(3);
    }

    [Fact]
    public async Task DeliverDueAsyncDailyLimitNearlySpentHoldsNoticesAndLetsLoginCodesThrough()
    {
        var store = Store();
        smtp.MaxPerDay = 100;
        await store.RecordSentAsync(Now.AddHours(-1), 85, Ct());
        await store.TryEnqueueAsync(Batch(EmailKind.SecurityAlert, "alerta@example.test"), Ct());
        await store.TryEnqueueAsync(Batch(EmailKind.AccessRecovery, "reset@example.test"), Ct());
        await store.TryEnqueueAsync(Batch("login@example.test"), Ct());
        var logger = new RecordingLogger<EmailOutboxDeliverer>();

        await Create(store, logger).DeliverDueAsync(Ct());

        transport
            .Sent.Select(message => message.ToAddress)
            .Should()
            .BeEquivalentTo("reset@example.test", "login@example.test");
        store.Messages.Should().ContainSingle().Which.ToAddress.Should().Be("alerta@example.test");
        logger.LevelEntries.Should().ContainSingle(entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task DeliverDueAsyncDailyLimitSpentClaimsNothing()
    {
        var store = Store();
        smtp.MaxPerDay = 100;
        await store.RecordSentAsync(Now.AddHours(-1), 100, Ct());
        await store.TryEnqueueAsync(Batch("login@example.test"), Ct());

        (await Create(store).DeliverDueAsync(Ct())).Should().Be(0);

        transport.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task DeliverDueAsyncRecipientOverItsShareWaitsWithoutSpendingAnAttempt()
    {
        var store = Store();
        guard.RecipientBurst = 1;
        guard.RecipientPerHour = 1;
        await store.TryEnqueueAsync(
            Batch(EmailKind.SecurityAlert, "socio@example.test") with
            {
                Topic = "security-alert:CredentialChanged",
            },
            Ct()
        );
        await store.TryEnqueueAsync(
            Batch(EmailKind.SecurityAlert, "socio@example.test") with
            {
                Topic = "security-alert:AuthenticatorEnabled",
            },
            Ct()
        );

        await Create(store).DeliverDueAsync(Ct());

        transport.Sent.Should().ContainSingle();
        var waiting = store.Messages.Should().ContainSingle().Subject;
        waiting.AttemptCount.Should().Be(0);
        waiting.NextAttemptAt.Should().Be(Now.AddHours(1));
        store.Rescheduled.Should().BeEmpty();
    }

    [Fact]
    public async Task DeliverDueAsyncCodesAreNeverPacedPerRecipient()
    {
        var store = Store();
        guard.RecipientBurst = 1;
        await store.TryEnqueueAsync(Batch("ana@example.test"), Ct());
        await store.TryEnqueueAsync(Batch("ana@example.test"), Ct());

        await Create(store).DeliverDueAsync(Ct());

        transport
            .Sent.Should()
            .HaveCount(2, "the guard already limited them when they were asked for");
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
    }

    [Fact]
    public async Task DeliverDueAsyncRecordingTheFailureFailsLeavesTheMessageToItsLease()
    {
        var store = new ThrowingOnWriteStore(protector) { EnqueuedAt = Now };
        await store.TryEnqueueAsync(Batch("ana@example.test"), Ct());
        transport.ThrowOnSend = new InvalidOperationException("Connection reset");
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
        var store = new ThrowingOnSweepStore(protector) { EnqueuedAt = Now };
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
        var store = new FakeEmailOutboxStore(shared) { EnqueuedAt = Now };
        await store.TryEnqueueAsync(
            BatchWithAttachment("uno@example.test", "dos@example.test", "tres@example.test"),
            Ct()
        );

        await Create(store, reader: shared).DeliverDueAsync(Ct());

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
    public async Task DeliverDueAsyncFailureAfterLosingTheLeaseLeavesTheReclaimedRowAlone()
    {
        var store = Store();
        await store.TryEnqueueAsync(Batch("ana@example.test"), Ct());
        var gate = new GatedFailingTransport();
        var lost = Create(store, with: gate);
        transport.ThrowOnSend = new InvalidOperationException("Connection reset");

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
            EmailQueueOptions.BulkPriority,
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

    private sealed class GatedFailingTransport : IEmailTransport, IEmailTransportSession
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

        public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            Reached.TrySetResult();
            await gate.Task;
            throw new InvalidOperationException("The connection dropped after the lease expired.");
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
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

    private sealed class BlockingEmailTransport : IEmailTransport, IEmailTransportSession
    {
        public Task<IEmailTransportSession> OpenAsync(CancellationToken ct = default)
        {
            return Task.FromResult<IEmailTransportSession>(this);
        }

        public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            await Task.Delay(Timeout.Infinite, ct);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
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
