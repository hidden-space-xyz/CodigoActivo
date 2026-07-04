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
        // Widen to long so a large in-range page (page is only floored at 1, never capped) can't
        // overflow Int32 into a negative Skip that PostgreSQL rejects with "OFFSET must not be
        // negative"; any page past the end simply yields no items.
        var skip = (long)(page - 1) * pageSize;
        var items =
            skip >= total
                ? []
                : await source.Skip((int)skip).Take(pageSize).ToListAsync(ct);
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
