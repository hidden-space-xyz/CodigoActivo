namespace CodigoActivo.Domain.Common;

/// <summary>
/// Materializes composed <see cref="IQueryable{T}"/> read queries. The abstraction lives in
/// Domain so the Application layer can stay free of any EF Core dependency: services compose the
/// query (Where/OrderBy/Select over the projections) and hand it here to be executed.
/// </summary>
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
