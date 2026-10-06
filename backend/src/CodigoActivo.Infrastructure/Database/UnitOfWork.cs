using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Domain.Common;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CodigoActivo.Infrastructure.Database;

/// <summary>
/// Commits what the repositories of the scoped <see cref="CodigoActivoDbContext"/> staged and
/// publishes the domain events of the committed aggregates afterwards: right after a save, or
/// when the explicit transaction the save ran in commits.
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

    private readonly List<IDomainEvent> uncommitted = [];

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var raised = PullDomainEvents();
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
            var tracked = context
                .ChangeTracker.Entries()
                .Select(entry => entry.Entity)
                .ToHashSet(ReferenceEqualityComparer.Instance);
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
                    uncommitted.Clear();
                    DetachAddedSince(tracked);
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

    private List<IDomainEvent> PullDomainEvents()
    {
        return
        [
            .. context
                .ChangeTracker.Entries<IHasDomainEvents>()
                .ToList()
                .SelectMany(entry => entry.Entity.PullDomainEvents()),
        ];
    }

    private void DetachAddedSince(HashSet<object> tracked)
    {
        var added = context
            .ChangeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added && !tracked.Contains(entry.Entity))
            .ToList();
        foreach (var entry in added)
        {
            entry.State = EntityState.Detached;
        }
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
}
