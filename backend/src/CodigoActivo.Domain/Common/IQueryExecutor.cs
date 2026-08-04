namespace CodigoActivo.Domain.Common;

public interface IQueryExecutor
{
    public Task<PagedResult<T>> ToPagedAsync<T>(
        IQueryable<T> source,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    public Task<IReadOnlyList<T>> ToListAsync<T>(IQueryable<T> source, CancellationToken ct = default);

    public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> source, CancellationToken ct = default);
}
