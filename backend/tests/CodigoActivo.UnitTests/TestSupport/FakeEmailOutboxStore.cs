using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Infrastructure.Communication;

namespace CodigoActivo.UnitTests.TestSupport;

public sealed record RescheduledMessage(
    Guid Id,
    int AttemptCount,
    DateTimeOffset NextAttemptAt,
    string? Error
);

/// <summary>
/// In-memory stand-in for the PostgreSQL outbox: it keeps the same rows, protects the stored content
/// with the supplied protector and claims by the same priority, schedule, lease and attempt rules, so
/// the delivery policy can be exercised without a database.
/// </summary>
public class FakeEmailOutboxStore(EmailOutboxProtector protector, int capacity = 100)
    : IEmailOutboxStore
{
    private readonly List<EmailOutboxMessage> messages = [];
    private readonly List<EmailOutboxContent> contents = [];
    private readonly List<RescheduledMessage> rescheduled = [];

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

    public Exception? ThrowOnClaim { get; set; }

    public Action? OnClaim { get; set; }

    public virtual Task<bool> TryEnqueueAsync(EmailBatch batch, CancellationToken ct = default)
    {
        lock (messages)
        {
            if (
                !EmailQueueOptions.IsCapacityExempt(batch.Kind)
                && messages.Count + batch.Recipients.Count > capacity
            )
            {
                return Task.FromResult(false);
            }

            var content = protector.ToContent(batch, DateTimeOffset.UnixEpoch);
            contents.Add(content);
            messages.AddRange(
                batch.Recipients.Select(recipient => new EmailOutboxMessage
                {
                    ContentId = content.Id,
                    Content = content,
                    Kind = batch.Kind,
                    Priority = EmailQueueOptions.PriorityOf(batch.Kind),
                    ToAddress = recipient.Address,
                    ToName = recipient.Name,
                    CreatedAt = DateTimeOffset.UnixEpoch,
                    NextAttemptAt = DateTimeOffset.UnixEpoch,
                })
            );
            return Task.FromResult(true);
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
            var claimed = messages
                .Where(message =>
                    message.NextAttemptAt <= now
                    && (message.LockedUntil is null || message.LockedUntil <= now)
                    && message.AttemptCount < EmailQueueOptions.MaxAttempts
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
        return new EmailOutboxMessage
        {
            Id = message.Id,
            ContentId = message.ContentId,
            Content = message.Content,
            Kind = message.Kind,
            Priority = message.Priority,
            ToAddress = message.ToAddress,
            ToName = message.ToName,
            CreatedAt = message.CreatedAt,
            AttemptCount = message.AttemptCount,
            NextAttemptAt = message.NextAttemptAt,
            LockedUntil = message.LockedUntil,
            LastError = message.LastError,
        };
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

    public virtual Task<IReadOnlyList<ExhaustedEmail>> RemoveExhaustedAsync(
        DateTimeOffset now,
        CancellationToken ct = default
    )
    {
        lock (messages)
        {
            var exhausted = messages
                .Where(message =>
                    EmailQueueOptions.IsExhausted(message.AttemptCount)
                    && (message.LockedUntil is null || message.LockedUntil <= now)
                )
                .ToList();

            foreach (var message in exhausted)
            {
                messages.Remove(message);
            }

            return Task.FromResult<IReadOnlyList<ExhaustedEmail>>([
                .. exhausted.Select(message => new ExhaustedEmail(
                    message.Kind,
                    message.AttemptCount
                )),
            ]);
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
}
