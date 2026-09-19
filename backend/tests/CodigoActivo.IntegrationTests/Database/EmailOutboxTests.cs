using System.Data.Common;
using System.Text;
using AwesomeAssertions;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodigoActivo.IntegrationTests.Database;

public sealed class EmailOutboxTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private const string Code = "482913";

    private IEmailOutboxStore Store => Factory.Services.GetRequiredService<IEmailOutboxStore>();

    private EmailQueueOptions Options => Factory.Services.GetRequiredService<EmailQueueOptions>();

    private static EmailBatch Batch(params string[] addresses)
    {
        return new EmailBatch(
            EmailKind.TwoFactorCode,
            "Tu código de acceso",
            $"<p>{Code}</p>",
            Code,
            [.. addresses.Select(address => new EmailRecipient(address, "Ana"))]
        );
    }

    private static EmailBatch NotificationBatch(params string[] addresses)
    {
        return new EmailBatch(
            EmailKind.ActivityNotification,
            "Nueva actividad",
            "<p>Hola</p>",
            "Hola",
            [.. addresses.Select(address => new EmailRecipient(address, "Ana"))]
        );
    }

    private static EmailBatch ManualBatchWithAttachment(params string[] addresses)
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

    private EmailOutboxDeliverer Deliverer()
    {
        return Factory.Services.GetRequiredService<EmailOutboxDeliverer>();
    }

    private Task<List<EmailOutboxMessage>> PendingAsync()
    {
        return Factory.QueryAsync(db =>
            db.EmailOutboxMessages.AsNoTracking().OrderBy(m => m.ToAddress).ToListAsync(Ct)
        );
    }

    [Fact]
    public async Task TryEnqueueAsyncUsesItsOwnUnitOfWorkAndNeverCommitsTheCallersChanges()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var callerContext = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();
        var staged = new FileEntity
        {
            Id = Guid.NewGuid(),
            Name = "never-committed",
            Extension = "png",
            UploadedAt = Factory.Clock.UtcNow,
            UploadedBy = TestSeedData.Users.AdminId,
        };
        callerContext.Files.Add(staged);

        var queued = await Store.TryEnqueueAsync(Batch("ana@example.test"), Ct);

        queued.Should().BeTrue();
        (await PendingAsync()).Should().ContainSingle();
        callerContext.ChangeTracker.Entries<FileEntity>().Should().NotBeEmpty();
        var persisted = await Factory.QueryAsync(db =>
            db.Files.AnyAsync(file => file.Id == staged.Id, Ct)
        );
        persisted.Should().BeFalse("the outbox writes through a database session of its own");
    }

    [Fact]
    public async Task TryEnqueueAsyncStoresNoReadableSubjectOrBody()
    {
        await Store.TryEnqueueAsync(Batch("ana@example.test"), Ct);

        var stored = await Factory.QueryAsync(db =>
            db.EmailOutboxContents.AsNoTracking().SingleAsync(Ct)
        );

        Encoding.UTF8.GetString(stored.Subject).Should().NotContain("código");
        Encoding.UTF8.GetString(stored.HtmlBody).Should().NotContain(Code);
        Encoding.UTF8.GetString(stored.TextBody).Should().NotContain(Code);

        var rows = await Factory.QueryAsync(db =>
            db.Database.SqlQuery<string>(
                    $"SELECT (subject || html_body || text_body)::text AS \"Value\" FROM email_outbox_contents"
                )
                .ToListAsync(Ct)
        );
        rows.Should().ContainSingle().Which.Should().NotContain(Code);
    }

    [Fact]
    public async Task TryEnqueueAsyncBeyondTheCapacityStoresNothing()
    {
        var capacity = Options.Capacity;
        try
        {
            Options.Capacity = 2;

            (await Store.TryEnqueueAsync(NotificationBatch("uno@example.test"), Ct))
                .Should()
                .BeTrue();
            (
                await Store.TryEnqueueAsync(
                    NotificationBatch("dos@example.test", "tres@example.test"),
                    Ct
                )
            )
                .Should()
                .BeFalse();

            (await PendingAsync()).Should().ContainSingle();
            var contents = await Factory.QueryAsync(db => db.EmailOutboxContents.CountAsync(Ct));
            contents.Should().Be(1, "a rejected batch leaves no content behind");
        }
        finally
        {
            Options.Capacity = capacity;
        }
    }

    [Fact]
    public async Task TryEnqueueAsyncWithoutRecipientsStoresNothing()
    {
        var queued = await Store.TryEnqueueAsync(
            new EmailBatch(EmailKind.Manual, "Asunto", "<p>Hola</p>", "Hola", []),
            Ct
        );

        queued.Should().BeTrue();
        (await PendingAsync()).Should().BeEmpty();
        var contents = await Factory.QueryAsync(db => db.EmailOutboxContents.CountAsync(Ct));
        contents.Should().Be(0);
    }

    [Fact]
    public async Task ClaimDueAsyncWithoutDueMessagesReturnsNothing()
    {
        var now = Factory.Clock.UtcNow;

        var claimed = await Store.ClaimDueAsync(now, now + Options.Lease, 10, Ct);

        claimed.Should().BeEmpty();
    }

    [Fact]
    public async Task RescheduleAsyncStoresATruncatedErrorAndClearsAnEmptyOne()
    {
        await Store.TryEnqueueAsync(Batch("ana@example.test"), Ct);
        var now = Factory.Clock.UtcNow;
        var message = (await Store.ClaimDueAsync(now, now + Options.Lease, 10, Ct))[0];
        var longError = new string('e', EmailOutboxMessage.LastErrorMaxLength + 50);

        await Store.RescheduleAsync(
            message.Id,
            message.AttemptCount,
            now.AddMinutes(1),
            longError,
            Ct
        );

        var stored = (await PendingAsync())[0];
        stored.LastError.Should().HaveLength(EmailOutboxMessage.LastErrorMaxLength);
        stored.AttemptCount.Should().Be(1, "the claim already counted the attempt");

        await Store.RescheduleAsync(message.Id, message.AttemptCount, now.AddMinutes(5), "   ", Ct);

        (await PendingAsync())[0].LastError.Should().BeNull();
    }

    [Fact]
    public async Task RescheduleAsyncOfAnExpiredLeaseLeavesTheReclaimedMessageAlone()
    {
        await Store.TryEnqueueAsync(Batch("ana@example.test"), Ct);
        var now = Factory.Clock.UtcNow;
        var lost = (await Store.ClaimDueAsync(now, now, 10, Ct))[0];
        var afterLease = now.AddSeconds(1);
        var reclaimed = (await Store.ClaimDueAsync(afterLease, afterLease + Options.Lease, 10, Ct))[
            0
        ];

        var rescheduled = await Store.RescheduleAsync(
            lost.Id,
            lost.AttemptCount,
            now.AddDays(1),
            "the worker that lost its lease",
            Ct
        );
        var removed = await Store.RemoveAsync(lost.Id, lost.AttemptCount, Ct);

        rescheduled.Should().Be(0, "the message moved on to the next attempt");
        removed.Should().Be(0);
        reclaimed.AttemptCount.Should().Be(2);
        var stored = (await PendingAsync()).Should().ContainSingle().Subject;
        stored.AttemptCount.Should().Be(2);
        stored.LastError.Should().BeNull();
        stored.LockedUntil.Should().Be(afterLease + Options.Lease);
    }

    [Fact]
    public async Task ClaimDueAsyncOrdersInteractiveMailBeforeOlderBulkMail()
    {
        await Store.TryEnqueueAsync(
            ManualBatchWithAttachment("uno@example.test", "dos@example.test"),
            Ct
        );
        Factory.Clock.UtcNow = Factory.Clock.UtcNow.AddMinutes(10);
        await Store.TryEnqueueAsync(Batch("ana@example.test"), Ct);
        var now = Factory.Clock.UtcNow;

        var claimed = await Store.ClaimDueAsync(now, now + Options.Lease, 1, Ct);

        claimed
            .Should()
            .ContainSingle("the batch size leaves room for one message only")
            .Which.Kind.Should()
            .Be(EmailKind.TwoFactorCode);
    }

    [Fact]
    public async Task ClaimDueAsyncSharedContentIsLoadedOncePerBatch()
    {
        await Store.TryEnqueueAsync(
            ManualBatchWithAttachment("uno@example.test", "dos@example.test", "tres@example.test"),
            Ct
        );
        var now = Factory.Clock.UtcNow;

        var claimed = await Store.ClaimDueAsync(now, now + Options.Lease, 10, Ct);

        claimed.Should().HaveCount(3);
        claimed
            .Select(message => message.Content)
            .Distinct()
            .Should()
            .ContainSingle("the shared bytes are materialized once for the whole batch");
        foreach (var message in claimed)
        {
            message.Content.Should().BeSameAs(claimed[0].Content);
            message.Content.Parts.Should().HaveCount(2);
        }
    }

    /// <summary>
    /// Stops the host in the narrowest window there is: the claim is already committed, so every row
    /// of the batch has spent an attempt, and the rows still have to be read out before anything can
    /// be delivered. The batch has to come back whole; a claim dropped here would sit out its lease
    /// with one attempt fewer and nobody would have tried to send it.
    /// </summary>
    [Fact]
    public async Task ClaimDueAsyncShutdownAfterTheClaimStillReturnsTheLeasedBatch()
    {
        await Store.TryEnqueueAsync(Batch("uno@example.test", "dos@example.test"), Ct);
        var protector = Factory.Services.GetRequiredService<EmailOutboxProtector>();
        using var shutdown = new CancellationTokenSource();
        await using var provider = BuildScopes(
            await ConnectionStringAsync(),
            new ShutdownAfterClaimInterceptor(shutdown)
        );
        var store = new EmailOutboxStore(
            provider.GetRequiredService<IServiceScopeFactory>(),
            protector,
            new EmailOutboxSignal(),
            Options,
            Factory.Clock
        );
        var now = Factory.Clock.UtcNow;

        var claimed = await store.ClaimDueAsync(now, now + Options.Lease, 10, shutdown.Token);

        shutdown
            .IsCancellationRequested.Should()
            .BeTrue("the host stopped as soon as the claim was committed");
        claimed
            .Select(message => message.ToAddress)
            .Should()
            .BeEquivalentTo("uno@example.test", "dos@example.test");
        claimed
            .Should()
            .AllSatisfy(message =>
            {
                message.AttemptCount.Should().Be(1);
                protector.Unprotect(message.Content).TextBody.Should().Be(Code);
            });
        (await PendingAsync())
            .Should()
            .AllSatisfy(message =>
            {
                message.AttemptCount.Should().Be(1, "the claim spent an attempt on every row");
                message.LockedUntil.Should().Be(now + Options.Lease);
            });
    }

    [Fact]
    public async Task DeliverDueAsyncSharedContentIsDeliveredToEveryRecipientOfTheBatch()
    {
        await Store.TryEnqueueAsync(
            ManualBatchWithAttachment("uno@example.test", "dos@example.test", "tres@example.test"),
            Ct
        );

        var delivered = await Deliverer().DeliverDueAsync(Ct);

        delivered.Should().Be(3);
        Factory
            .EmailSender.Sent.Should()
            .HaveCount(3)
            .And.AllSatisfy(message =>
                message
                    .Attachments.Should()
                    .ContainSingle()
                    .Which.Content.Should()
                    .Equal("contenido"u8.ToArray())
            );
        (await PendingAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task DeliverDueAsyncAbandonedLastAttemptRemovesTheMessageAndItsContent()
    {
        await Store.TryEnqueueAsync(ManualBatchWithAttachment("ana@example.test"), Ct);

        for (var attempt = 1; attempt <= EmailQueueOptions.MaxAttempts; attempt++)
        {
            var now = Factory.Clock.UtcNow;
            var claimed = await Store.ClaimDueAsync(now, now + Options.Lease, 10, Ct);

            claimed
                .Should()
                .ContainSingle("nothing finished the attempt, so the lease is all that held it")
                .Which.AttemptCount.Should()
                .Be(attempt);
            Factory.Clock.UtcNow = now + Options.Lease + TimeSpan.FromSeconds(1);
        }

        var delivered = await Deliverer().DeliverDueAsync(Ct);

        delivered.Should().Be(0, "a message out of attempts is never claimed again");
        (await PendingAsync()).Should().BeEmpty();
        Factory.EmailSender.Sent.Should().BeEmpty();
        var leftovers = await Factory.QueryAsync(db =>
            Task.FromResult(
                (
                    Contents: db.EmailOutboxContents.Count(),
                    Parts: db.EmailOutboxContentParts.Count()
                )
            )
        );
        leftovers.Contents.Should().Be(0);
        leftovers.Parts.Should().Be(0);
    }

    [Fact]
    public async Task DeliverDueAsyncEmptyOutboxStillClearsTheOrphanContent()
    {
        await Factory.SeedAsync(db =>
        {
            db.EmailOutboxContents.Add(
                new EmailOutboxContent
                {
                    Subject = [1],
                    HtmlBody = [2],
                    TextBody = [3],
                    CreatedAt = Factory.Clock.UtcNow,
                }
            );
            return Task.CompletedTask;
        });

        var delivered = await Deliverer().DeliverDueAsync(Ct);

        delivered.Should().Be(0);
        var contents = await Factory.QueryAsync(db => db.EmailOutboxContents.CountAsync(Ct));
        contents.Should().Be(0, "content nothing references any more never has to survive a run");
    }

    [Fact]
    public async Task TryEnqueueAsyncFullOutboxStillAcceptsTheMailSomebodyIsWaitingFor()
    {
        var capacity = Options.Capacity;
        try
        {
            Options.Capacity = 1;
            (await Store.TryEnqueueAsync(Batch("uno@example.test"), Ct)).Should().BeTrue();

            var notification = await Store.TryEnqueueAsync(
                NotificationBatch("dos@example.test"),
                Ct
            );
            var manual = await Store.TryEnqueueAsync(
                ManualBatchWithAttachment("tres@example.test"),
                Ct
            );
            var loginCode = await Store.TryEnqueueAsync(Batch("cuatro@example.test"), Ct);

            notification.Should().BeFalse("automatic mail waits for room");
            manual.Should().BeFalse("a manual batch waits for room");
            loginCode.Should().BeTrue("a login code is never held back by a full outbox");
            (await PendingAsync())
                .Select(message => message.ToAddress)
                .Should()
                .BeEquivalentTo("uno@example.test", "cuatro@example.test");
        }
        finally
        {
            Options.Capacity = capacity;
        }
    }

    [Fact]
    public async Task ClaimDueAsyncTwoConcurrentProcessorsNeverTakeTheSameMessage()
    {
        await Store.TryEnqueueAsync(
            Batch(
                "uno@example.test",
                "dos@example.test",
                "tres@example.test",
                "cuatro@example.test"
            ),
            Ct
        );
        var now = Factory.Clock.UtcNow;

        var claims = await Task.WhenAll(
            Store.ClaimDueAsync(now, now + Options.Lease, 4, Ct),
            Store.ClaimDueAsync(now, now + Options.Lease, 4, Ct)
        );

        var ids = claims.SelectMany(claim => claim.Select(message => message.Id)).ToList();
        ids.Should().OnlyHaveUniqueItems().And.HaveCount(4);
    }

    [Fact]
    public async Task DeliverDueAsyncTwoConcurrentRunsDeliverEveryMessageExactlyOnce()
    {
        await Store.TryEnqueueAsync(
            Batch("uno@example.test", "dos@example.test", "tres@example.test"),
            Ct
        );

        await Task.WhenAll(Deliverer().DeliverDueAsync(Ct), Deliverer().DeliverDueAsync(Ct));

        Factory
            .EmailSender.Sent.Select(m => m.ToAddress)
            .Should()
            .BeEquivalentTo("uno@example.test", "dos@example.test", "tres@example.test");
        (await PendingAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ClaimDueAsyncLeasedMessageIsSkippedUntilTheLeaseExpires()
    {
        await Store.TryEnqueueAsync(Batch("ana@example.test"), Ct);
        var now = Factory.Clock.UtcNow;
        var leaseUntil = now + Options.Lease;

        (await Store.ClaimDueAsync(now, leaseUntil, 10, Ct)).Should().ContainSingle();
        (await Store.ClaimDueAsync(now, leaseUntil, 10, Ct))
            .Should()
            .BeEmpty("the message is leased to the first claim");

        var afterLease = leaseUntil.AddSeconds(1);
        var reclaimed = await Store.ClaimDueAsync(afterLease, afterLease + Options.Lease, 10, Ct);

        reclaimed
            .Should()
            .ContainSingle("a process that died mid-send must not block the message forever")
            .Which.AttemptCount.Should()
            .Be(2, "a claim nothing finished still spent an attempt");
    }

    [Fact]
    public async Task DeliverDueAsyncFiveFailuresRemoveTheMessageAndItsSharedContent()
    {
        Factory.EmailSender.ThrowOnSend = new InvalidOperationException("Connection refused");
        await Store.TryEnqueueAsync(Batch("ana@example.test"), Ct);

        for (var attempt = 1; attempt <= EmailQueueOptions.MaxAttempts; attempt++)
        {
            await Deliverer().DeliverDueAsync(Ct);

            var pending = await PendingAsync();
            if (attempt == EmailQueueOptions.MaxAttempts)
            {
                pending.Should().BeEmpty("the fifth failure discards the message");
                break;
            }

            var message = pending.Should().ContainSingle().Subject;
            message.AttemptCount.Should().Be(attempt);
            message.LockedUntil.Should().BeNull();
            message.LastError.Should().Contain("Connection refused");
            message
                .NextAttemptAt.Should()
                .Be(Factory.Clock.UtcNow + EmailQueueOptions.RetryDelayAfter(attempt));

            Factory.Clock.UtcNow = message.NextAttemptAt;
        }

        Factory.EmailSender.Sent.Should().BeEmpty();
        var leftovers = await Factory.QueryAsync(db =>
            Task.FromResult(
                (
                    Contents: db.EmailOutboxContents.Count(),
                    Parts: db.EmailOutboxContentParts.Count()
                )
            )
        );
        leftovers.Contents.Should().Be(0, "a discarded message leaves no stored content");
        leftovers.Parts.Should().Be(0);
    }

    [Fact]
    public async Task DeliverDueAsyncSharedAttachmentIsStoredOnceAndRemovedWithTheLastMessage()
    {
        Factory.EmailSender.FailFor("berto@example.test");
        await Store.TryEnqueueAsync(
            ManualBatchWithAttachment("ana@example.test", "berto@example.test"),
            Ct
        );

        var stored = await Factory.QueryAsync(db =>
            Task.FromResult(
                (
                    Messages: db.EmailOutboxMessages.Count(),
                    Contents: db.EmailOutboxContents.Count(),
                    Parts: db.EmailOutboxContentParts.Count()
                )
            )
        );
        stored.Messages.Should().Be(2);
        stored.Contents.Should().Be(1, "both recipients share one stored copy");
        stored.Parts.Should().Be(2, "the attachment and the inline image are stored once each");

        await Deliverer().DeliverDueAsync(Ct);

        (await PendingAsync())
            .Should()
            .ContainSingle()
            .Which.ToAddress.Should()
            .Be("berto@example.test");
        var afterFirst = await Factory.QueryAsync(db =>
            Task.FromResult(
                (
                    Contents: db.EmailOutboxContents.Count(),
                    Parts: db.EmailOutboxContentParts.Count()
                )
            )
        );
        afterFirst.Contents.Should().Be(1, "the pending recipient still needs the content");
        afterFirst.Parts.Should().Be(2);

        Factory.EmailSender.Clear();
        Factory.Clock.UtcNow = Factory.Clock.UtcNow.AddHours(1);
        await Deliverer().DeliverDueAsync(Ct);

        (await PendingAsync()).Should().BeEmpty();
        var afterLast = await Factory.QueryAsync(db =>
            Task.FromResult(
                (
                    Contents: db.EmailOutboxContents.Count(),
                    Parts: db.EmailOutboxContentParts.Count()
                )
            )
        );
        afterLast.Contents.Should().Be(0);
        afterLast.Parts.Should().Be(0, "the shared bytes leave with the last message");
        Factory
            .EmailSender.Sent.Should()
            .ContainSingle()
            .Which.Attachments.Should()
            .ContainSingle()
            .Which.Content.Should()
            .Equal("contenido"u8.ToArray());
    }

    [Fact]
    public async Task DeliverDueAsyncDeliveredMessageLeavesAnEmptyOutbox()
    {
        await Store.TryEnqueueAsync(ManualBatchWithAttachment("ana@example.test"), Ct);

        var delivered = await Deliverer().DeliverDueAsync(Ct);

        delivered.Should().Be(1);
        var message = Factory.EmailSender.Sent.Should().ContainSingle().Subject;
        message.Subject.Should().Be("Asamblea general");
        message.InlineImages.Should().ContainSingle().Which.ContentId.Should().Be("logo");
        (await PendingAsync()).Should().BeEmpty();
        var contents = await Factory.QueryAsync(db => db.EmailOutboxContents.CountAsync(Ct));
        contents.Should().Be(0);
    }

    [Fact]
    public async Task LoginTwoFactorCodeIsDeliveredThroughTheOutboxAndRetriedAfterAFailure()
    {
        var client = CreateClient();
        Factory.EmailSender.ThrowOnSend = new InvalidOperationException("Connection refused");

        await PassPasswordStepAsync(client, TestSeedData.MemberCredentials);

        var queued = (await PendingAsync()).Should().ContainSingle().Subject;
        queued.Kind.Should().Be(EmailKind.TwoFactorCode);
        queued.ToAddress.Should().Be(TestSeedData.MemberEmail);
        queued.AttemptCount.Should().Be(1, "the first attempt failed while the code stayed stored");
        Factory.EmailSender.Sent.Should().BeEmpty();

        Factory.EmailSender.ThrowOnSend = null;
        Factory.Clock.UtcNow = queued.NextAttemptAt;
        await Factory.DrainEmailOutboxAsync();

        (await PendingAsync()).Should().BeEmpty();
        var code = Factory.EmailSender.LastLoginCodeSentTo(TestSeedData.MemberEmail);
        await CompleteTwoFactorAsync(client, code);
    }

    private Task<string> ConnectionStringAsync()
    {
        return Factory.QueryAsync(db => Task.FromResult(db.Database.GetConnectionString()!));
    }

    private static ServiceProvider BuildScopes(string connectionString, IInterceptor interceptor)
    {
        return new ServiceCollection()
            .AddDbContext<CodigoActivoDbContext>(options =>
                options
                    .UseNpgsql(connectionString)
                    .UseSnakeCaseNamingConvention()
                    .AddInterceptors(interceptor)
            )
            .BuildServiceProvider();
    }

    /// <summary>
    /// Cancels the supplied source before the first command the outbox runs after its claim, which is
    /// the read that materializes the claimed rows.
    /// </summary>
    private sealed class ShutdownAfterClaimInterceptor(CancellationTokenSource shutdown)
        : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default
        )
        {
            shutdown.Cancel();
            return ValueTask.FromResult(result);
        }
    }
}
