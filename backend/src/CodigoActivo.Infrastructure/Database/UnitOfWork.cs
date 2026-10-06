using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Domain.Common;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CodigoActivo.Infrastructure.Database;

/// <summary>
/// Commits what the repositories of the scoped <see cref="CodigoActivoDbContext"/> staged and
/// publishes the domain events of the committed aggregates afterwards: right after a save, or
/// when the explicit transaction the save ran in commits. The events of a change stay with it
/// until a commit includes it, so a failed save or transaction neither publishes nor loses them.
/// </summary>
/// <param name="context">Database context shared by the repositories of the scope.</param>
/// <param name="publisher">Publisher of the committed domain events.</param>
/// <param name="logger">Logger of retried transactions.</param>
public sealed class UnitOfWork(
    CodigoActivoDbContext context,
    IDomainEventPublisher publisher,
    ILogger<UnitOfWork> logger
) : IUnitOfWork
{
    /// <summary>
    /// Attempts made when PostgreSQL resolves a deadlock by aborting a transaction, which happens
    /// when a concurrent transaction takes the same row locks in the opposite order, as a signup of
    /// a household being erased does.
    /// </summary>
    internal const int MaxTransactionAttempts = 3;

    private readonly List<IDomainEvent> staged = [];
    private readonly List<IDomainEvent> uncommitted = [];

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var sources = TrackedAggregates();
        int saved;
        try
        {
            saved = await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException
                    is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } violation
            )
        {
            throw new UniqueConstraintViolationException(EntityTypeOf(violation.TableName), ex);
        }

        List<IDomainEvent> raised =
        [
            .. staged,
            .. sources.SelectMany(source => source.PullDomainEvents()),
        ];
        staged.Clear();
        if (context.Database.CurrentTransaction is null)
        {
            await publisher.PublishAsync(raised, ct);
        }
        else
        {
            uncommitted.AddRange(raised);
        }

        return saved;
    }

    /// <inheritdoc />
    public void DiscardChanges()
    {
        context.ChangeTracker.Clear();
        staged.Clear();
    }

    /// <inheritdoc />
    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> work,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(work);

        if (context.Database.CurrentTransaction is not null)
        {
            return await work(ct);
        }

        for (var attempt = 1; ; attempt++)
        {
            var start = CaptureAttemptStart();
            T result;
            await using (var transaction = await context.Database.BeginTransactionAsync(ct))
            {
                try
                {
                    result = await work(ct);
                    await transaction.CommitAsync(ct);
                }
                catch (Exception ex)
                {
                    RestoreAttemptStart(start);
                    if (attempt >= MaxTransactionAttempts || !IsDeadlock(ex))
                    {
                        throw;
                    }

                    logger.TransactionDeadlockRetried(attempt);
                    continue;
                }
            }

            var committed = uncommitted.ToList();
            uncommitted.Clear();
            await publisher.PublishAsync(committed, ct);
            return result;
        }
    }

    private List<IHasDomainEvents> TrackedAggregates()
    {
        return [.. context.ChangeTracker.Entries<IHasDomainEvents>().Select(entry => entry.Entity)];
    }

    private AttemptStart CaptureAttemptStart()
    {
        staged.AddRange(TrackedAggregates().SelectMany(aggregate => aggregate.PullDomainEvents()));
        return new AttemptStart(
            [.. context.ChangeTracker.Entries().Select(TrackedEntry.Of)],
            [.. staged]
        );
    }

    private void RestoreAttemptStart(AttemptStart start)
    {
        uncommitted.Clear();
        foreach (
            var aggregate in TrackedAggregates()
                .Concat(start.Entries.Select(entry => entry.Entity).OfType<IHasDomainEvents>())
        )
        {
            aggregate.PullDomainEvents();
        }

        var known = start
            .Entries.Select(entry => entry.Entity)
            .ToHashSet(ReferenceEqualityComparer.Instance);
        foreach (
            var entry in context
                .ChangeTracker.Entries()
                .Where(entry => !known.Contains(entry.Entity))
                .ToList()
        )
        {
            entry.State = EntityState.Detached;
        }

        foreach (var entry in start.Entries)
        {
            entry.Restore(context);
        }

        staged.Clear();
        staged.AddRange(start.Staged);
    }

    private static bool IsDeadlock(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            if (exception is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected })
            {
                return true;
            }
        }

        return false;
    }

    private Type? EntityTypeOf(string? tableName)
    {
        return context
            .Model.GetEntityTypes()
            .FirstOrDefault(entityType =>
                string.Equals(entityType.GetTableName(), tableName, StringComparison.Ordinal)
            )
            ?.ClrType;
    }

    private sealed record AttemptStart(
        IReadOnlyList<TrackedEntry> Entries,
        IReadOnlyList<IDomainEvent> Staged
    );

    private sealed record TrackedEntry(
        object Entity,
        EntityState State,
        PropertyValues CurrentValues,
        PropertyValues? OriginalValues
    )
    {
        public static TrackedEntry Of(EntityEntry entry)
        {
            return new TrackedEntry(
                entry.Entity,
                entry.State,
                entry.CurrentValues.Clone(),
                entry.State is EntityState.Added ? null : entry.OriginalValues.Clone()
            );
        }

        public void Restore(DbContext context)
        {
            var entry = context.Entry(Entity);
            if (OriginalValues is null)
            {
                entry.State = EntityState.Added;
                entry.CurrentValues.SetValues(CurrentValues);
                return;
            }

            entry.State = EntityState.Unchanged;
            entry.CurrentValues.SetValues(CurrentValues);
            entry.State = EntityState.Unchanged;
            entry.OriginalValues.SetValues(OriginalValues);
            foreach (var property in entry.Properties)
            {
                if (
                    !property
                        .Metadata.GetValueComparer()
                        .Equals(property.CurrentValue, property.OriginalValue)
                )
                {
                    property.IsModified = true;
                }
            }

            if (State is EntityState.Deleted)
            {
                entry.State = EntityState.Deleted;
            }
        }
    }
}
