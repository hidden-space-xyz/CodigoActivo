using CodigoActivo.Domain.Common;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Repositories.Abstractions;

/// <summary>
/// Stores the instances of an aggregate in the write-side database context, tracked so the unit of
/// work saves every change made through the aggregate.
/// </summary>
/// <typeparam name="TAggregate">Root of the aggregate the repository stores.</typeparam>
/// <param name="context">Database context used for persistence.</param>
public abstract class AggregateRepository<TAggregate>(CodigoActivoDbContext context)
    : IRepository<TAggregate>
    where TAggregate : class, IAggregateRoot
{
    /// <summary>
    /// Gets the database context the aggregates are tracked in.
    /// </summary>
    protected CodigoActivoDbContext Context { get; } = context;

    /// <summary>
    /// Gets the set of stored aggregates.
    /// </summary>
    protected DbSet<TAggregate> Set { get; } = context.Set<TAggregate>();

    /// <inheritdoc />
    public async Task AddAsync(TAggregate aggregate, CancellationToken ct = default)
    {
        await Set.AddAsync(aggregate, ct);
    }

    /// <inheritdoc />
    public void Remove(TAggregate aggregate)
    {
        Set.Remove(aggregate);
    }
}
