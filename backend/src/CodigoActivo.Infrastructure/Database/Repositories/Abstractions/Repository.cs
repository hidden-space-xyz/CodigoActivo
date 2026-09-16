using System.Linq.Expressions;
using CodigoActivo.Domain.Entities.Abstractions;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Repositories.Abstractions;

/// <summary>
/// Persists and retrieves entity data from the database.
/// </summary>
/// <typeparam name="TEntity">Type of entity stored by the repository.</typeparam>
/// <param name="context">Database context used for persistence.</param>
public abstract class Repository<TEntity>(CodigoActivoDbContext context) : IDbRepository<TEntity>
    where TEntity : IdentifiableEntity
{
    /// <summary>
    /// Gets the context value.
    /// </summary>
    protected CodigoActivoDbContext Context { get; } = context;
    /// <summary>
    /// Gets the set value.
    /// </summary>
    protected DbSet<TEntity> Set { get; } = context.Set<TEntity>();

    /// <summary>
    /// Creates a query for the stored entities without tracking changes.
    /// </summary>
    /// <returns>The resulting t entity value.</returns>
    public IQueryable<TEntity> Query()
    {
        return Set.AsNoTracking();
    }

    /// <summary>
    /// Finds the first entity that satisfies the supplied predicate.
    /// </summary>
    /// <param name="predicate">Condition that an entity must satisfy.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching t entity, or <see langword="null"/> when it is not found.</returns>
    public async Task<TEntity?> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default
    )
    {
        return await Set.FirstOrDefaultAsync(predicate, ct);
    }

    /// <summary>
    /// Gets the requested entity.
    /// </summary>
    /// <param name="predicate">Condition that an entity must satisfy.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching t entity items.</returns>
    public async Task<IReadOnlyList<TEntity>> GetAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default
    )
    {
        return await Set.AsNoTracking().Where(predicate).ToListAsync(ct);
    }

    /// <summary>
    /// Counts entities that satisfy the optional predicate.
    /// </summary>
    /// <param name="predicate">Condition that an entity must satisfy.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an int.</returns>
    public Task<int> CountAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default
    )
    {
        return Set.CountAsync(predicate, ct);
    }

    /// <summary>
    /// Determines whether any entity satisfies the supplied predicate.
    /// </summary>
    /// <param name="predicate">Condition that an entity must satisfy.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public Task<bool> ExistsAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default
    )
    {
        return Set.AnyAsync(predicate, ct);
    }

    /// <summary>
    /// Adds an entity to the current unit of work.
    /// </summary>
    /// <param name="entity">Entity to persist or remove.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task AddAsync(TEntity entity, CancellationToken ct = default)
    {
        await Set.AddAsync(entity, ct);
    }

    /// <summary>
    /// Removes the selected entity from persistent storage.
    /// </summary>
    /// <param name="entity">Entity to persist or remove.</param>
    public void Remove(TEntity entity)
    {
        Set.Remove(entity);
    }

    /// <summary>
    /// Removes the selected entity from persistent storage.
    /// </summary>
    /// <param name="predicate">Condition that an entity must satisfy.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an int.</returns>
    public async Task<int> RemoveAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default
    )
    {
        return await Set.Where(predicate).ExecuteDeleteAsync(ct);
    }

    /// <summary>
    /// Sets the exclusive featured state.
    /// </summary>
    /// <typeparam name="TFeaturable">Type used for featurable.</typeparam>
    /// <param name="set">The set value.</param>
    /// <param name="id">Identifier of the target entity.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    protected static async Task<bool> SetExclusiveFeaturedAsync<TFeaturable>(
        DbSet<TFeaturable> set,
        Guid id,
        CancellationToken ct
    )
        where TFeaturable : IdentifiableEntity, IFeaturable
    {
        if (!await set.AnyAsync(e => e.Id == id, ct))
        {
            return false;
        }

        await set.Where(e => EF.Property<bool>(e, nameof(IFeaturable.Featured)) || e.Id == id)
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(
                        e => EF.Property<bool>(e, nameof(IFeaturable.Featured)),
                        e => e.Id == id
                    ),
                ct
            );
        return true;
    }
}
