using System.Data.Common;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Describes a message that was removed because its attempts were spent, with the data the worker is
/// allowed to log about it.
/// </summary>
/// <param name="Kind">Email category whose limits were applied.</param>
/// <param name="AttemptCount">Number of delivery attempts that were started.</param>
public sealed record ExhaustedEmail(EmailKind Kind, int AttemptCount);

/// <summary>
/// Reads and writes the email outbox rows the delivery worker works with.
/// </summary>
public interface IEmailOutboxStore : IEmailOutbox
{
    /// <summary>
    /// Takes up to <paramref name="batchSize"/> messages whose next attempt is due, in priority
    /// order, leasing them until <paramref name="leaseUntil"/> so no other instance delivers the same
    /// row and counting the attempt as started. Messages that share stored content share one
    /// instance of it.
    /// </summary>
    /// <param name="now">Current timestamp compared against the schedule and the leases.</param>
    /// <param name="leaseUntil">Timestamp until which the claimed rows stay reserved.</param>
    /// <param name="batchSize">Largest number of messages to claim.</param>
    /// <param name="ct">
    /// Cancellation token that stops the claim itself. Once the claim is committed the attempt is
    /// already spent, so the claimed rows are read and returned even when it is cancelled: a batch
    /// dropped on the way out would wait for its lease to expire with one attempt fewer.
    /// </param>
    /// <returns>A task whose result contains the claimed email outbox message items.</returns>
    public Task<IReadOnlyList<EmailOutboxMessage>> ClaimDueAsync(
        DateTimeOffset now,
        DateTimeOffset leaseUntil,
        int batchSize,
        CancellationToken ct = default
    );

    /// <summary>
    /// Removes a message that was delivered or that ran out of attempts, as long as it is still on
    /// the attempt the caller claimed.
    /// </summary>
    /// <param name="messageId">Identifier of the message.</param>
    /// <param name="attemptCount">Attempt the caller claimed, which must still be the current one.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the number of deleted rows.</returns>
    public Task<int> RemoveAsync(Guid messageId, int attemptCount, CancellationToken ct = default);

    /// <summary>
    /// Releases the lease of a failed message so it is retried later, as long as it is still on the
    /// attempt the caller claimed: a worker whose lease expired never overwrites the schedule of the
    /// worker that claimed the message afterwards.
    /// </summary>
    /// <param name="messageId">Identifier of the message.</param>
    /// <param name="attemptCount">Attempt the caller claimed, which must still be the current one.</param>
    /// <param name="nextAttemptAt">Timestamp from which the message may be attempted again.</param>
    /// <param name="error">Truncated diagnostic of the failure, without recipient data.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the number of updated rows.</returns>
    public Task<int> RescheduleAsync(
        Guid messageId,
        int attemptCount,
        DateTimeOffset nextAttemptAt,
        string? error,
        CancellationToken ct = default
    );

    /// <summary>
    /// Removes the messages whose attempts are spent and whose lease has expired, which is how a
    /// message abandoned during its last attempt leaves the outbox.
    /// </summary>
    /// <param name="now">Current timestamp compared against the leases.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the removed messages.</returns>
    public Task<IReadOnlyList<ExhaustedEmail>> RemoveExhaustedAsync(
        DateTimeOffset now,
        CancellationToken ct = default
    );

    /// <summary>
    /// Removes the stored content, and with it the stored attachments, that no pending message
    /// references any more.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the number of deleted rows.</returns>
    public Task<int> RemoveOrphanContentAsync(CancellationToken ct = default);
}

/// <summary>
/// Stores outbound email in PostgreSQL. Every call uses a scope, and therefore a database session, of
/// its own: enqueuing commits before returning and never carries the pending changes of the handler
/// that asked for the email.
/// </summary>
/// <param name="scopes">Scope factory used to resolve the context of each operation.</param>
/// <param name="protector">Protector applied to the stored subject, bodies and attachments.</param>
/// <param name="signal">Signal that wakes the delivery worker of this process.</param>
/// <param name="options">Configuration values used by the component.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class EmailOutboxStore(
    IServiceScopeFactory scopes,
    EmailOutboxProtector protector,
    EmailOutboxSignal signal,
    EmailQueueOptions options,
    IClock clock
) : IEmailOutboxStore
{
    private const string ClaimSql = """
        UPDATE email_outbox_messages
        SET locked_until = @lease,
            attempt_count = attempt_count + 1
        WHERE id IN (
            SELECT id
            FROM email_outbox_messages
            WHERE next_attempt_at <= @now
              AND (locked_until IS NULL OR locked_until <= @now)
              AND attempt_count < @attempts
            ORDER BY priority, next_attempt_at
            LIMIT @batch
            FOR UPDATE SKIP LOCKED
        )
        RETURNING id
        """;

    private const string RemoveExhaustedSql = """
        DELETE FROM email_outbox_messages
        WHERE id IN (
            SELECT id
            FROM email_outbox_messages
            WHERE attempt_count >= @attempts
              AND (locked_until IS NULL OR locked_until <= @now)
            FOR UPDATE SKIP LOCKED
        )
        RETURNING kind, attempt_count
        """;

    /// <inheritdoc />
    public async Task<bool> TryEnqueueAsync(EmailBatch batch, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (batch.Recipients.Count is 0)
        {
            return true;
        }

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        if (!EmailQueueOptions.IsCapacityExempt(batch.Kind))
        {
            var pending = await db.EmailOutboxMessages.CountAsync(ct);
            if (pending + batch.Recipients.Count > options.Capacity)
            {
                await transaction.RollbackAsync(ct);
                return false;
            }
        }

        var now = clock.UtcNow;
        var content = protector.ToContent(batch, now);
        var priority = EmailQueueOptions.PriorityOf(batch.Kind);
        await db.EmailOutboxContents.AddAsync(content, ct);
        await db.EmailOutboxMessages.AddRangeAsync(
            batch.Recipients.Select(recipient => new EmailOutboxMessage
            {
                ContentId = content.Id,
                Kind = batch.Kind,
                Priority = priority,
                ToAddress = recipient.Address,
                ToName = recipient.Name,
                CreatedAt = now,
                AttemptCount = 0,
                NextAttemptAt = now,
            }),
            ct
        );

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        signal.Notify();
        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmailOutboxMessage>> ClaimDueAsync(
        DateTimeOffset now,
        DateTimeOffset leaseUntil,
        int batchSize,
        CancellationToken ct = default
    )
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();

        var claimed = await ClaimIdsAsync(db, now, leaseUntil, batchSize, ct);
        if (claimed.Count is 0)
        {
            return [];
        }

        var messages = await db
            .EmailOutboxMessages.AsNoTracking()
            .Where(message => claimed.Contains(message.Id))
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
    public async Task<IReadOnlyList<ExhaustedEmail>> RemoveExhaustedAsync(
        DateTimeOffset now,
        CancellationToken ct = default
    )
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();

        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = RemoveExhaustedSql;
            AddParameter(command, "now", now.ToUniversalTime());
            AddParameter(command, "attempts", EmailQueueOptions.MaxAttempts);

            var removed = new List<ExhaustedEmail>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                removed.Add(
                    new ExhaustedEmail(
                        Enum.Parse<EmailKind>(reader.GetString(0)),
                        reader.GetInt32(1)
                    )
                );
            }

            return removed;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
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

    private static async Task<List<Guid>> ClaimIdsAsync(
        CodigoActivoDbContext db,
        DateTimeOffset now,
        DateTimeOffset leaseUntil,
        int batchSize,
        CancellationToken ct
    )
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = ClaimSql;
            AddParameter(command, "now", now.ToUniversalTime());
            AddParameter(command, "lease", leaseUntil.ToUniversalTime());
            AddParameter(command, "batch", batchSize);
            AddParameter(command, "attempts", EmailQueueOptions.MaxAttempts);

            var claimed = new List<Guid>(batchSize);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                claimed.Add(reader.GetGuid(0));
            }

            return claimed;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
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
