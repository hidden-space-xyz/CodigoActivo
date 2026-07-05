using System.Linq.Expressions;
using CodigoActivo.Domain.Entities.Abstractions;

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
    void Remove(TEntity entity);

    /// <summary>Stages every entity matching the predicate for removal and returns how many matched.</summary>
    Task<int> RemoveAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
}