using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Infrastructure.Communication;

namespace CodigoActivo.UnitTests.TestSupport;

public sealed record RescheduledMessage(
    Guid Id,
    int AttemptCount,
    DateTimeOffset NextAttemptAt,
    string? Error
);

public sealed record ReleasedMessage(Guid Id, int AttemptCount, DateTimeOffset NextAttemptAt);

/// <summary>
/// In-memory stand-in for the PostgreSQL outbox: it keeps the same rows, protects the stored content
/// with the supplied protector and claims, replaces, expires and counts by the same rules, so the
/// delivery policy can be exercised without a database.
/// </summary>
public class FakeEmailOutboxStore(EmailOutboxProtector protector, int capacity = 100)
    : IEmailOutboxStore
{
    private readonly List<EmailOutboxMessage> messages = [];
    private readonly List<EmailOutboxContent> contents = [];
    private readonly List<RescheduledMessage> rescheduled = [];
    private readonly List<ReleasedMessage> released = [];
    private readonly List<(DateTimeOffset At, int Sent)> sentRecords = [];

    public IReadOnlyList<EmailOutboxMessage> Messages
    {
        get
        {
            lock (messages)
            {
                return [.. messages];
            }
        }
    }

    public IReadOnlyList<EmailOutboxContent> Contents
    {
        get
        {
            lock (messages)
            {
                return [.. contents];
            }
        }
    }

    public IReadOnlyList<RescheduledMessage> Rescheduled
    {
        get
        {
            lock (messages)
            {
                return [.. rescheduled];
            }
        }
    }

    public IReadOnlyList<ReleasedMessage> Released
    {
        get
        {
            lock (messages)
            {
                return [.. released];
            }
        }
    }

    public int SentRecorded
    {
        get
        {
            lock (messages)
            {
                return sentRecords.Sum(record => record.Sent);
            }
        }
    }

    public DateTimeOffset EnqueuedAt { get; set; } = DateTimeOffset.UnixEpoch;

    public Exception? ThrowOnClaim { get; set; }

    public Action? OnClaim { get; set; }

    public int? LastClaimMaxPriority { get; private set; }

    public virtual Task<EmailEnqueueResult> TryEnqueueAsync(
        EmailBatch batch,
        CancellationToken ct = default
    )
    {
        lock (messages)
        {
            if (
                EmailQueueOptions.IsCapped(batch.Kind)
                && messages.Count(message => message.Priority == EmailQueueOptions.BulkPriority)
                    + batch.Recipients.Count
                    > capacity
            )
            {
                return Task.FromResult(EmailEnqueueResult.Full);
            }

            if (batch.Topic is { } topic)
            {
                messages.RemoveAll(message =>
                    message.Topic == topic
                    && batch.Recipients.Any(recipient => recipient.Address == message.ToAddress)
                    && (message.LockedUntil is null || message.LockedUntil <= EnqueuedAt)
                );
            }

            var lifetime = batch.ValidFor ?? EmailQueueOptions.LifetimeOf(batch.Kind);
            var content = protector.ToContent(batch, EnqueuedAt);
            contents.Add(content);
            messages.AddRange(
                batch.Recipients.Select(recipient => new EmailOutboxMessage
                {
                    ContentId = content.Id,
                    Content = content,
                    Kind = batch.Kind,
                    Priority = EmailQueueOptions.PriorityOf(batch.Kind),
                    Topic = batch.Topic,
                    ToAddress = recipient.Address,
                    ToName = recipient.Name,
                    CreatedAt = EnqueuedAt,
                    ExpiresAt = lifetime is { } span ? EnqueuedAt + span : null,
                    NextAttemptAt = EnqueuedAt,
                })
            );
            return Task.FromResult(EmailEnqueueResult.Queued);
        }
    }

    public Task<bool> AnyAsync(string topic, CancellationToken ct = default)
    {
        lock (messages)
        {
            return Task.FromResult(messages.Exists(message => message.Topic == topic));
        }
    }

    public void SetAttemptCount(int attemptCount)
    {
        lock (messages)
        {
            foreach (var message in messages)
            {
                message.AttemptCount = attemptCount;
            }
        }
    }

    public virtual Task<IReadOnlyList<EmailOutboxMessage>> ClaimDueAsync(
        DateTimeOffset now,
        DateTimeOffset leaseUntil,
        int batchSize,
        int maxPriority,
        CancellationToken ct = default
    )
    {
        OnClaim?.Invoke();
        if (ThrowOnClaim is not null)
        {
            throw ThrowOnClaim;
        }

        lock (messages)
        {
            LastClaimMaxPriority = maxPriority;
            var claimed = messages
                .Where(message =>
                    message.NextAttemptAt <= now
                    && (message.LockedUntil is null || message.LockedUntil <= now)
                    && (message.ExpiresAt is null || message.ExpiresAt > now)
                    && message.Priority <= maxPriority
                )
                .OrderBy(message => message.Priority)
                .ThenBy(message => message.NextAttemptAt)
                .Take(batchSize)
                .ToList();

            foreach (var message in claimed)
            {
                message.LockedUntil = leaseUntil;
                message.AttemptCount++;
            }

            return Task.FromResult<IReadOnlyList<EmailOutboxMessage>>([.. claimed.Select(Claimed)]);
        }
    }

    /// <summary>
    /// Copies a claimed row the way the database does, so a worker sees the attempt it claimed and
    /// never the changes another worker makes afterwards. The stored content stays shared, because
    /// one claim reads it once for the whole batch.
    /// </summary>
    private static EmailOutboxMessage Claimed(EmailOutboxMessage message)
    {
        var claimed = new EmailOutboxMessage
        {
            ContentId = message.ContentId,
            Content = message.Content,
            Kind = message.Kind,
            Priority = message.Priority,
            Topic = message.Topic,
            ToAddress = message.ToAddress,
            ToName = message.ToName,
            CreatedAt = message.CreatedAt,
            ExpiresAt = message.ExpiresAt,
            AttemptCount = message.AttemptCount,
            NextAttemptAt = message.NextAttemptAt,
            LockedUntil = message.LockedUntil,
            LastError = message.LastError,
        };
        Persisted.Overwrite(claimed, new { message.Id });
        return claimed;
    }

    public virtual Task<int> RemoveAsync(
        Guid messageId,
        int attemptCount,
        CancellationToken ct = default
    )
    {
        lock (messages)
        {
            return Task.FromResult(
                messages.RemoveAll(message =>
                    message.Id == messageId && message.AttemptCount == attemptCount
                )
            );
        }
    }

    public virtual Task<int> RescheduleAsync(
        Guid messageId,
        int attemptCount,
        DateTimeOffset nextAttemptAt,
        string? error,
        CancellationToken ct = default
    )
    {
        lock (messages)
        {
            var message = messages.Find(candidate =>
                candidate.Id == messageId && candidate.AttemptCount == attemptCount
            );
            if (message is null)
            {
                return Task.FromResult(0);
            }

            message.NextAttemptAt = nextAttemptAt;
            message.LockedUntil = null;
            message.LastError = error;
            rescheduled.Add(new RescheduledMessage(messageId, attemptCount, nextAttemptAt, error));
            return Task.FromResult(1);
        }
    }

    public virtual Task<int> ReleaseAsync(
        Guid messageId,
        int attemptCount,
        DateTimeOffset nextAttemptAt,
        CancellationToken ct = default
    )
    {
        lock (messages)
        {
            var message = messages.Find(candidate =>
                candidate.Id == messageId && candidate.AttemptCount == attemptCount
            );
            if (message is null)
            {
                return Task.FromResult(0);
            }

            message.NextAttemptAt = nextAttemptAt;
            message.LockedUntil = null;
            message.AttemptCount = attemptCount - 1;
            released.Add(new ReleasedMessage(messageId, attemptCount, nextAttemptAt));
            return Task.FromResult(1);
        }
    }

    public virtual Task<IReadOnlyList<EmailKind>> RemoveExpiredAsync(
        DateTimeOffset now,
        CancellationToken ct = default
    )
    {
        lock (messages)
        {
            var expired = messages
                .Where(message =>
                    message.ExpiresAt <= now
                    && (message.LockedUntil is null || message.LockedUntil <= now)
                )
                .ToList();

            foreach (var message in expired)
            {
                messages.Remove(message);
            }

            return Task.FromResult<IReadOnlyList<EmailKind>>([
                .. expired.Select(message => message.Kind),
            ]);
        }
    }

    public virtual Task<int> CountOverdueAsync(
        DateTimeOffset acceptedBefore,
        CancellationToken ct = default
    )
    {
        lock (messages)
        {
            return Task.FromResult(
                messages.Count(message =>
                    message.ExpiresAt is null && message.CreatedAt < acceptedBefore
                )
            );
        }
    }

    public virtual Task<int> RemoveOrphanContentAsync(CancellationToken ct = default)
    {
        lock (messages)
        {
            return Task.FromResult(
                contents.RemoveAll(content =>
                    !messages.Exists(message => message.ContentId == content.Id)
                )
            );
        }
    }

    public virtual Task RecordSentAsync(
        DateTimeOffset now,
        int sent,
        CancellationToken ct = default
    )
    {
        lock (messages)
        {
            sentRecords.Add((now, sent));
            return Task.CompletedTask;
        }
    }

    public virtual Task<long> CountSentSinceAsync(
        DateTimeOffset since,
        CancellationToken ct = default
    )
    {
        lock (messages)
        {
            return Task.FromResult(
                (long)sentRecords.Where(record => record.At >= since).Sum(record => record.Sent)
            );
        }
    }
}
