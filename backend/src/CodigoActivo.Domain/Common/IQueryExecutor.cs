namespace CodigoActivo.Domain.Common;

public interface IQueryExecutor
{
    Task<PagedResult<T>> ToPagedAsync<T>(
        IQueryable<T> source,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<T>> ToListAsync<T>(IQueryable<T> source, CancellationToken ct = default);

    Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> source, CancellationToken ct = default);
}
