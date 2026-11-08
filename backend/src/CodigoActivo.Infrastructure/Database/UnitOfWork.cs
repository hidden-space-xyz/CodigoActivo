using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Domain.Common;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CodigoActivo.Infrastructure.Database;

/// <summary>
/// Commits what the repositories of the scoped <see cref="CodigoActivoDbContext"/> staged and
/// publishes the domain events of the committed aggregates afterwards: right after a save, or
/// when the explicit transaction the save ran in commits. The events of a change stay with it
/// until a commit includes it: a refused save neither publishes nor loses them, and a transaction
/// that rolls back drops them with the changes it undid. Repositories that write a change on their
/// own hand its events over the same way.
/// </summary>
/// <param name="context">Database context shared by the repositories of the scope.</param>
/// <param name="publisher">Publisher of the committed domain events.</param>
public sealed class UnitOfWork(CodigoActivoDbContext context, IDomainEventPublisher publisher)
    : IUnitOfWork
{
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
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrentUpdateException(
                ex.Entries is [var changed, ..] ? changed.Metadata.ClrType : null,
                ex
            );
        }
        catch (DbUpdateException ex)
            when (ex.InnerException
                    is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } violation
            )
        {
            throw new UniqueConstraintViolationException(EntityTypeOf(violation.TableName), ex);
        }

        await PublishAfterCommitAsync([.. sources.SelectMany(source => source.PullDomainEvents())]);
        return saved;
    }

    /// <summary>
    /// Publishes the events of a change already written to the database: right away when no
    /// transaction is open, or when the open unit-of-work transaction commits. A transaction that
    /// rolls back discards them.
    /// </summary>
    /// <param name="domainEvents">
    /// Events of the written change, in the order they were raised.
    /// </param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal async Task PublishAfterCommitAsync(IReadOnlyList<IDomainEvent> domainEvents)
    {
        if (context.Database.CurrentTransaction is null)
        {
            await publisher.PublishAsync(domainEvents);
            return;
        }

        uncommitted.AddRange(domainEvents);
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
                if (IsDeadlock(ex))
                {
                    throw new ConcurrentUpdateException(entityType: null, ex);
                }

                throw;
            }
        }

        var committed = uncommitted.ToList();
        uncommitted.Clear();
        await publisher.PublishAsync(committed);
        return result;
    }

    private List<IHasDomainEvents> TrackedAggregates()
    {
        return [.. context.ChangeTracker.Entries<IHasDomainEvents>().Select(entry => entry.Entity)];
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
