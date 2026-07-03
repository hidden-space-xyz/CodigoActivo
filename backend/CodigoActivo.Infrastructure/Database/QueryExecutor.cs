using CodigoActivo.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database;

/// <summary>
/// EF Core implementation of <see cref="IQueryExecutor"/>. This is the single place where read
/// queries built in the Application layer are turned into database round-trips.
/// </summary>
public sealed class QueryExecutor : IQueryExecutor
{
    public async Task<PagedResult<T>> ToPagedAsync<T>(
        IQueryable<T> source,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var total = await source.CountAsync(ct);
        var items = await source.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<T>(items, total, page, pageSize);
    }

    public async Task<IReadOnlyList<T>> ToListAsync<T>(
        IQueryable<T> source,
        CancellationToken ct = default
    )
    {
        return await source.ToListAsync(ct);
    }

    public async Task<T?> FirstOrDefaultAsync<T>(
        IQueryable<T> source,
        CancellationToken ct = default
    )
    {
        return await source.FirstOrDefaultAsync(ct);
    }
}
