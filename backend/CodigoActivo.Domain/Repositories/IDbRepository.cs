using CodigoActivo.Domain.Entities.Abstractions;
using System.Linq.Expressions;

namespace CodigoActivo.Domain.Repositories;

public interface IDbRepository<TEntity>
    where TEntity : IdentifiableEntity
{
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
