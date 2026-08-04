using System.Linq.Expressions;
using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Repositories;

public interface IDbRepository<TEntity>
    where TEntity : IdentifiableEntity
{
    public IQueryable<TEntity> Query();

    public Task<TEntity?> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default
    );

    public Task<IReadOnlyList<TEntity>> GetAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default
    );

    public Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);

    public Task<bool> ExistsAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default
    );

    public Task AddAsync(TEntity entity, CancellationToken ct = default);
    public void Remove(TEntity entity);

    public Task<int> RemoveAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default
    );
}
