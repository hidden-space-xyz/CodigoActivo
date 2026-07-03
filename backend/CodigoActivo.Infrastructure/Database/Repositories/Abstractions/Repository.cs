using CodigoActivo.Domain.Entities.Abstractions;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CodigoActivo.Infrastructure.Database.Repositories.Abstractions;

public abstract class Repository<TEntity>(CodigoActivoDbContext context) : IDbRepository<TEntity>
    where TEntity : IdentifiableEntity
{
    protected CodigoActivoDbContext Context { get; } = context;
    protected DbSet<TEntity> Set { get; } = context.Set<TEntity>();

    public IQueryable<TEntity> Query()
    {
        return Set.AsNoTracking();
    }

    public virtual async Task<TEntity?> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default
    )
    {
        return await Set.FirstOrDefaultAsync(predicate, ct);
    }

    public virtual async Task<IReadOnlyList<TEntity>> GetAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default
    )
    {
        return await Set.AsNoTracking().Where(predicate).ToListAsync(ct);
    }

    public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default)
    {
        return await Set.AsNoTracking().ToListAsync(ct);
    }

    public Task<int> CountAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default
    )
    {
        return Set.CountAsync(predicate, ct);
    }

    public Task<bool> ExistsAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default
    )
    {
        return Set.AnyAsync(predicate, ct);
    }

    public async Task AddAsync(TEntity entity, CancellationToken ct = default)
    {
        await Set.AddAsync(entity, ct);
    }

    public async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
    {
        await Set.AddRangeAsync(entities, ct);
    }

    public void Update(TEntity entity)
    {
        Set.Update(entity);
    }

    public void UpdateRange(IEnumerable<TEntity> entities)
    {
        Set.UpdateRange(entities);
    }

    public virtual async Task RemoveAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default
    )
    {
        var entities = await Set.Where(predicate).ToListAsync(ct);
        Set.RemoveRange(entities);
    }
}
