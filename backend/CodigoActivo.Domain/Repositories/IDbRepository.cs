using System.Linq.Expressions;
using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Repositories;

public interface IDbRepository<TEntity>
    where TEntity : IdentifiableEntity
{
    /// <summary>
    /// A non-tracked, non-materialized query over the entity set. Read endpoints project
    /// and compose OData query options ($filter/$orderby/$top/$skip/$count) on top of it,
    /// so the whole pipeline translates to a single SQL query.
    /// </summary>
    IQueryable<TEntity> Query();

    Task<TEntity?> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<TEntity>> GetAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default);
    Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
    Task<bool> ExistsAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default
    );
    Task AddAsync(TEntity entity, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);
    void Update(TEntity entity);
    void UpdateRange(IEnumerable<TEntity> entities);
    Task RemoveAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
}
