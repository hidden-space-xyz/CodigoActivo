namespace CodigoActivo.Domain.Common;

/// <summary>
/// Defines the operations required to work with query executor.
/// </summary>
public interface IQueryExecutor
{
    /// <summary>
    /// Converts the value to paged.
    /// </summary>
    /// <typeparam name="T">Type of item processed by the operation.</typeparam>
    /// <param name="source">Source sequence to query.</param>
    /// <param name="page">One-based page number to return.</param>
    /// <param name="pageSize">Maximum number of items in the page.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a paged t.</returns>
    public Task<PagedResult<T>> ToPagedAsync<T>(
        IQueryable<T> source,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    /// <summary>
    /// Converts the value to list.
    /// </summary>
    /// <typeparam name="T">Type of item processed by the operation.</typeparam>
    /// <param name="source">Source sequence to query.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching t items.</returns>
    public Task<IReadOnlyList<T>> ToListAsync<T>(IQueryable<T> source, CancellationToken ct = default);

    /// <summary>
    /// Returns the first matching query result, or <see langword="null"/> when none exists.
    /// </summary>
    /// <typeparam name="T">Type of item processed by the operation.</typeparam>
    /// <param name="source">Source sequence to query.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching t, or <see langword="null"/> when it is not found.</returns>
    public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> source, CancellationToken ct = default);
}
