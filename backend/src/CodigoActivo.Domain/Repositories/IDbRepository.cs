using System.Linq.Expressions;
using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Repositories;

/// <summary>
/// Persists and retrieves entity data from the database.
/// </summary>
/// <typeparam name="TEntity">Type of entity stored by the repository.</typeparam>
public interface IDbRepository<TEntity>
    where TEntity : IdentifiableEntity
{
    /// <summary>
    /// Creates a query for the stored entities without tracking changes.
    /// </summary>
    /// <returns>The resulting t entity value.</returns>
    public IQueryable<TEntity> Query();

    /// <summary>
    /// Finds the first entity that satisfies the supplied predicate.
    /// </summary>
    /// <param name="predicate">Condition that an entity must satisfy.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching t entity, or <see langword="null"/> when it is not found.</returns>
    public Task<TEntity?> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default
    );

    /// <summary>
    /// Gets the requested entity.
    /// </summary>
    /// <param name="predicate">Condition that an entity must satisfy.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching t entity items.</returns>
    public Task<IReadOnlyList<TEntity>> GetAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default
    );

    /// <summary>
    /// Counts entities that satisfy the optional predicate.
    /// </summary>
    /// <param name="predicate">Condition that an entity must satisfy.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an int.</returns>
    public Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);

    /// <summary>
    /// Determines whether any entity satisfies the supplied predicate.
    /// </summary>
    /// <param name="predicate">Condition that an entity must satisfy.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public Task<bool> ExistsAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default
    );

    /// <summary>
    /// Adds an entity to the current unit of work.
    /// </summary>
    /// <param name="entity">Entity to persist or remove.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task AddAsync(TEntity entity, CancellationToken ct = default);
    /// <summary>
    /// Removes the selected entity from persistent storage.
    /// </summary>
    /// <param name="entity">Entity to persist or remove.</param>
    public void Remove(TEntity entity);

    /// <summary>
    /// Removes the selected entity from persistent storage.
    /// </summary>
    /// <param name="predicate">Condition that an entity must satisfy.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an int.</returns>
    public Task<int> RemoveAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default
    );
}
