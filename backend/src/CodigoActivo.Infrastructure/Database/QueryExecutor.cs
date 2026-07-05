using CodigoActivo.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database;

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
