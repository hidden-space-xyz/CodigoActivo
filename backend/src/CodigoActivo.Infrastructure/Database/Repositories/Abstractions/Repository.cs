using System.Linq.Expressions;
using CodigoActivo.Domain.Entities.Abstractions;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

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

    public void Remove(TEntity entity)
    {
        Set.Remove(entity);
    }

    public virtual async Task<int> RemoveAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default
    )
    {
        return await Set.Where(predicate).ExecuteDeleteAsync(ct);
    }

    protected static async Task<bool> SetExclusiveFeaturedAsync<TFeaturable>(
        DbSet<TFeaturable> set,
        Guid id,
        CancellationToken ct
    )
        where TFeaturable : IdentifiableEntity, IFeaturable
    {
        if (!await set.AnyAsync(e => e.Id == id, ct))
            return false;

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
