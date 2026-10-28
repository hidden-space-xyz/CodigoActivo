using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Reads and writes the email outbox rows the delivery worker works with.
/// </summary>
public interface IEmailOutboxStore : IEmailOutbox, IPendingEmails
{
    /// <summary>
    /// Takes up to <paramref name="batchSize"/> unexpired messages whose next attempt is due and whose
    /// priority is at most <paramref name="maxPriority"/>, by priority and then by schedule, leasing
    /// them until <paramref name="leaseUntil"/> so no other instance delivers the same row and
    /// counting the attempt as started. Rows another transaction has locked are skipped. The messages
    /// come back in claim order with their content and its parts loaded, and messages that share
    /// stored content share one instance of it.
    /// </summary>
    /// <param name="now">Current timestamp compared against the schedule, the leases and the expiry.</param>
    /// <param name="leaseUntil">Timestamp until which the claimed rows stay reserved.</param>
    /// <param name="batchSize">Largest number of messages to claim.</param>
    /// <param name="maxPriority">Highest stored priority value that may be claimed.</param>
    /// <param name="ct">
    /// Cancellation token that stops the claim itself. Once the claim is committed the attempt is
    /// already spent, so the claimed rows are read and returned even when it is cancelled: a batch
    /// dropped on the way out would wait for its lease to expire.
    /// </param>
    /// <returns>
    /// A task whose result contains the claimed messages, untracked, each with the attempt count it
    /// was claimed on.
    /// </returns>
    public Task<IReadOnlyList<EmailOutboxMessage>> ClaimDueAsync(
        DateTimeOffset now,
        DateTimeOffset leaseUntil,
        int batchSize,
        int maxPriority,
        CancellationToken ct = default
    );

    /// <summary>
    /// Removes a message that was delivered or rejected for good, as long as it is still on the
    /// attempt the caller claimed. Its content stays until <see cref="RemoveOrphanContentAsync"/>.
    /// </summary>
    /// <param name="messageId">Identifier of the message.</param>
    /// <param name="attemptCount">Attempt the caller claimed, which must still be the current one.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the number of deleted rows: zero when the message is gone or was
    /// claimed again.
    /// </returns>
    public Task<int> RemoveAsync(Guid messageId, int attemptCount, CancellationToken ct = default);

    /// <summary>
    /// Releases the lease of a failed message so it is retried later and stores the error, as long as
    /// it is still on the attempt the caller claimed: a worker whose lease expired never overwrites the
    /// schedule of the worker that claimed the message afterwards.
    /// </summary>
    /// <param name="messageId">Identifier of the message.</param>
    /// <param name="attemptCount">Attempt the caller claimed, which must still be the current one.</param>
    /// <param name="nextAttemptAt">Timestamp from which the message may be attempted again.</param>
    /// <param name="error">
    /// Diagnostic of the failure, without recipient data; it is cut to
    /// <see cref="EmailOutboxMessage.LastErrorMaxLength"/> and a blank one is stored as
    /// <see langword="null"/>.
    /// </param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the number of updated rows: zero when the message is gone or was
    /// claimed again.
    /// </returns>
    public Task<int> RescheduleAsync(
        Guid messageId,
        int attemptCount,
        DateTimeOffset nextAttemptAt,
        string? error,
        CancellationToken ct = default
    );

    /// <summary>
    /// Hands back a message the worker claimed but did not try, because the delivery paused or its
    /// recipient has to wait: the lease is released and the attempt returned, as long as it is still
    /// on the attempt the caller claimed.
    /// </summary>
    /// <param name="messageId">Identifier of the message.</param>
    /// <param name="attemptCount">Attempt the caller claimed, which must still be the current one.</param>
    /// <param name="nextAttemptAt">Timestamp from which the message may be claimed again.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the number of updated rows: zero when the message is gone or was
    /// claimed again.
    /// </returns>
    public Task<int> ReleaseAsync(
        Guid messageId,
        int attemptCount,
        DateTimeOffset nextAttemptAt,
        CancellationToken ct = default
    );

    /// <summary>
    /// Removes the messages that expired and whose lease, if any, has passed. Rows another
    /// transaction has locked are left for a later call.
    /// </summary>
    /// <param name="now">Current timestamp compared against the expiry and the leases.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the kind of every removed message, for the log.</returns>
    public Task<IReadOnlyList<EmailKind>> RemoveExpiredAsync(
        DateTimeOffset now,
        CancellationToken ct = default
    );

    /// <summary>
    /// Counts the messages that never expire and were accepted before a moment.
    /// </summary>
    /// <param name="acceptedBefore">Moment the messages were accepted before.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the number of such messages.</returns>
    public Task<int> CountOverdueAsync(
        DateTimeOffset acceptedBefore,
        CancellationToken ct = default
    );

    /// <summary>
    /// Removes the stored content, and with it the stored attachments, that no pending message
    /// references any more.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the number of deleted content rows; the parts the database
    /// deletes with them are not counted.
    /// </returns>
    public Task<int> RemoveOrphanContentAsync(CancellationToken ct = default);

    /// <summary>
    /// Adds messages handed to the SMTP server to the count of their UTC hour and removes the counts
    /// older than two <see cref="EmailQueueOptions.DailyWindow"/>.
    /// </summary>
    /// <param name="now">Time the messages were handed over.</param>
    /// <param name="sent">Number of messages handed over.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task RecordSentAsync(DateTimeOffset now, int sent, CancellationToken ct = default);

    /// <summary>
    /// Counts the messages handed to the SMTP server from the start of the UTC hour of a moment on,
    /// which counts up to one hour more than asked and so stays on the safe side.
    /// </summary>
    /// <param name="since">Start of the window.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the number of messages.</returns>
    public Task<long> CountSentSinceAsync(DateTimeOffset since, CancellationToken ct = default);
}

/// <summary>
/// Stores outbound email in PostgreSQL. Every call uses a scope, and therefore a database session, of
/// its own: enqueuing commits before returning and never carries the pending changes of the handler
/// that asked for the email. A stored batch wakes the delivery worker of this process.
/// </summary>
/// <param name="scopes">Scope factory used to resolve the context of each operation.</param>
/// <param name="protector">Protector applied to the stored subject, bodies and attachments.</param>
/// <param name="signal">Signal that wakes the delivery worker of this process.</param>
/// <param name="serverLimits">Largest message the SMTP server accepts, when it said so.</param>
/// <param name="options">Outbox policy, read for the cap on administrator-written mail.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class EmailOutboxStore(
    IServiceScopeFactory scopes,
    EmailOutboxProtector protector,
    EmailOutboxSignal signal,
    SmtpServerLimits serverLimits,
    EmailQueueOptions options,
    IClock clock
) : IEmailOutboxStore
{
    /// <inheritdoc />
    public async Task<EmailEnqueueResult> TryEnqueueAsync(
        EmailBatch batch,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (batch.Recipients.Count is 0)
        {
            return EmailEnqueueResult.Queued;
        }

        if (!serverLimits.Accepts(batch))
        {
            return EmailEnqueueResult.TooLarge;
        }

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var now = clock.UtcNow;

        if (EmailQueueOptions.IsCapped(batch.Kind))
        {
            var pending = await db.EmailOutboxMessages.CountAsync(
                message => message.Priority == EmailQueueOptions.BulkPriority,
                ct
            );
            if (pending + batch.Recipients.Count > options.Capacity)
            {
                await transaction.RollbackAsync(ct);
                return EmailEnqueueResult.Full;
            }
        }

        if (batch.Topic is { } topic)
        {
            await ReplacePendingAsync(db, topic, batch.Recipients, now, ct);
        }

        var content = protector.ToContent(batch, now);
        var priority = EmailQueueOptions.PriorityOf(batch.Kind);
        var lifetime = batch.ValidFor ?? EmailQueueOptions.LifetimeOf(batch.Kind);
        DateTimeOffset? expiresAt = lifetime is { } span ? now + span : null;
        await db.EmailOutboxContents.AddAsync(content, ct);
        await db.EmailOutboxMessages.AddRangeAsync(
            batch.Recipients.Select(recipient => new EmailOutboxMessage
            {
                ContentId = content.Id,
                Kind = batch.Kind,
                Priority = priority,
                Topic = batch.Topic,
                ToAddress = recipient.Address,
                ToName = recipient.Name,
                UnsubscribeUrl = recipient.UnsubscribeUrl,
                OneClickUnsubscribeUrl = recipient.OneClickUnsubscribeUrl,
                CreatedAt = now,
                ExpiresAt = expiresAt,
                AttemptCount = 0,
                NextAttemptAt = now,
            }),
            ct
        );

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        signal.Notify();
        return EmailEnqueueResult.Queued;
    }

    /// <inheritdoc />
    public async Task<bool> AnyAsync(EmailKind kind, CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();

        return await db.EmailOutboxMessages.AnyAsync(message => message.Kind == kind, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmailOutboxMessage>> ClaimDueAsync(
        DateTimeOffset now,
        DateTimeOffset leaseUntil,
        int batchSize,
        int maxPriority,
        CancellationToken ct = default
    )
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();

        var claimed = await ClaimIdsAsync(db, now, leaseUntil, batchSize, maxPriority, ct);
        if (claimed.Count is 0)
        {
            return [];
        }

        var messages = await db
            .EmailOutboxMessages.AsNoTracking()
            .Where(message => claimed.Contains(message.Id))
            .OrderBy(message => message.Priority)
            .ThenBy(message => message.NextAttemptAt)
            .ToListAsync(CancellationToken.None);

        var contentIds = messages.Select(message => message.ContentId).Distinct().ToList();
        var contents = await db
            .EmailOutboxContents.AsNoTracking()
            .Include(content => content.Parts)
            .Where(content => contentIds.Contains(content.Id))
            .ToDictionaryAsync(content => content.Id, CancellationToken.None);

        foreach (var message in messages)
        {
            message.Content = contents[message.ContentId];
        }

        return messages;
    }

    /// <inheritdoc />
    public async Task<int> RemoveAsync(
        Guid messageId,
        int attemptCount,
        CancellationToken ct = default
    )
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();

        return await db
            .EmailOutboxMessages.Where(message =>
                message.Id == messageId && message.AttemptCount == attemptCount
            )
            .ExecuteDeleteAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> RescheduleAsync(
        Guid messageId,
        int attemptCount,
        DateTimeOffset nextAttemptAt,
        string? error,
        CancellationToken ct = default
    )
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();

        var truncated = Truncate(error);

        return await db
            .EmailOutboxMessages.Where(message =>
                message.Id == messageId && message.AttemptCount == attemptCount
            )
            .ExecuteUpdateAsync(
                update =>
                    update
                        .SetProperty(message => message.NextAttemptAt, nextAttemptAt)
                        .SetProperty(message => message.LockedUntil, (DateTimeOffset?)null)
                        .SetProperty(message => message.LastError, truncated),
                ct
            );
    }

    /// <inheritdoc />
    public async Task<int> ReleaseAsync(
        Guid messageId,
        int attemptCount,
        DateTimeOffset nextAttemptAt,
        CancellationToken ct = default
    )
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();

        return await db
            .EmailOutboxMessages.Where(message =>
                message.Id == messageId && message.AttemptCount == attemptCount
            )
            .ExecuteUpdateAsync(
                update =>
                    update
                        .SetProperty(message => message.NextAttemptAt, nextAttemptAt)
                        .SetProperty(message => message.LockedUntil, (DateTimeOffset?)null)
                        .SetProperty(message => message.AttemptCount, attemptCount - 1),
                ct
            );
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmailKind>> RemoveExpiredAsync(
        DateTimeOffset now,
        CancellationToken ct = default
    )
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();

        var due = now.ToUniversalTime();
        FormattableString sql = $"""
            DELETE FROM email_outbox_messages
            WHERE id IN (
                SELECT id
                FROM email_outbox_messages
                WHERE expires_at <= {due}
                  AND (locked_until IS NULL OR locked_until <= {due})
                FOR UPDATE SKIP LOCKED
            )
            RETURNING *
            """;
        var removed = await db.EmailOutboxMessages.FromSql(sql).AsNoTracking().ToListAsync(ct);

        return [.. removed.Select(message => message.Kind)];
    }

    /// <inheritdoc />
    public async Task<int> CountOverdueAsync(
        DateTimeOffset acceptedBefore,
        CancellationToken ct = default
    )
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();

        return await db.EmailOutboxMessages.CountAsync(
            message => message.ExpiresAt == null && message.CreatedAt < acceptedBefore,
            ct
        );
    }

    /// <inheritdoc />
    public async Task<int> RemoveOrphanContentAsync(CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();

        return await db
            .EmailOutboxContents.Where(content =>
                !db.EmailOutboxMessages.Any(message => message.ContentId == content.Id)
            )
            .ExecuteDeleteAsync(ct);
    }

    /// <inheritdoc />
    public async Task RecordSentAsync(DateTimeOffset now, int sent, CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();

        var hour = HourOf(now);
        var oldest = HourOf(now - (2 * EmailQueueOptions.DailyWindow));
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO email_delivery_hours (hour, sent) VALUES ({hour}, {sent})
            ON CONFLICT (hour) DO UPDATE SET sent = email_delivery_hours.sent + EXCLUDED.sent;
            DELETE FROM email_delivery_hours WHERE hour < {oldest}
            """,
            ct
        );
    }

    /// <inheritdoc />
    public async Task<long> CountSentSinceAsync(
        DateTimeOffset since,
        CancellationToken ct = default
    )
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();

        var from = HourOf(since);
        return await db
            .EmailDeliveryHours.Where(hour => hour.Hour >= from)
            .SumAsync(hour => (long)hour.Sent, ct);
    }

    private static async Task ReplacePendingAsync(
        CodigoActivoDbContext db,
        string topic,
        IReadOnlyList<EmailRecipient> recipients,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        var addresses = recipients
            .Select(recipient => recipient.Address)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        foreach (var address in addresses)
        {
            var key = $"{topic}\n{address}";
            await db.Database.ExecuteSqlAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))",
                ct
            );
        }

        await db
            .EmailOutboxMessages.Where(message =>
                message.Topic == topic
                && addresses.Contains(message.ToAddress)
                && (message.LockedUntil == null || message.LockedUntil <= now)
            )
            .ExecuteDeleteAsync(ct);
    }

    private static Task<List<Guid>> ClaimIdsAsync(
        CodigoActivoDbContext db,
        DateTimeOffset now,
        DateTimeOffset leaseUntil,
        int batchSize,
        int maxPriority,
        CancellationToken ct
    )
    {
        var due = now.ToUniversalTime();
        FormattableString sql = $"""
            UPDATE email_outbox_messages
            SET locked_until = {leaseUntil.ToUniversalTime()},
                attempt_count = attempt_count + 1
            WHERE id IN (
                SELECT id
                FROM email_outbox_messages
                WHERE next_attempt_at <= {due}
                  AND (locked_until IS NULL OR locked_until <= {due})
                  AND (expires_at IS NULL OR expires_at > {due})
                  AND priority <= {maxPriority}
                ORDER BY priority, next_attempt_at
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
            )
            RETURNING id AS "Value"
            """;
        return db.Database.SqlQuery<Guid>(sql).ToListAsync(ct);
    }

    private static DateTimeOffset HourOf(DateTimeOffset moment)
    {
        var utc = moment.UtcDateTime;
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
    }

    private static string? Truncate(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return null;
        }

        return error.Length <= EmailOutboxMessage.LastErrorMaxLength
            ? error
            : error[..EmailOutboxMessage.LastErrorMaxLength];
    }
}
