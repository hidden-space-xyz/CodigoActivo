namespace CodigoActivo.Application.Abstractions.Querying;

/// <summary>
/// Runs against the database the queries composed over the <see cref="IReadStore"/> sources, so the
/// use cases never depend on the asynchronous operators of the data access library.
/// </summary>
public interface IQueryExecutor
{
    /// <summary>
    /// Counts the query and loads one page of it in its own order; a page past the end loads no item
    /// and still reports the total.
    /// </summary>
    /// <typeparam name="T">Type of the query results.</typeparam>
    /// <param name="source">Query to page, in the order the pages follow.</param>
    /// <param name="page">One-based page number to return.</param>
    /// <param name="pageSize">Maximum number of items in the page.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the items of the page, the total count and the page asked for.
    /// </returns>
    public Task<PagedResult<T>> ToPagedAsync<T>(
        IQueryable<T> source,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    /// <summary>
    /// Runs the query and loads every result.
    /// </summary>
    /// <typeparam name="T">Type of the query results.</typeparam>
    /// <param name="source">Query to run.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains every result, in the order of the query.</returns>
    public Task<IReadOnlyList<T>> ToListAsync<T>(
        IQueryable<T> source,
        CancellationToken ct = default
    );

    /// <summary>
    /// Runs the query and loads only its first result, in the order of the query.
    /// </summary>
    /// <typeparam name="T">Type of the query results.</typeparam>
    /// <param name="source">Query to run.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the first result, or the default value of
    /// <typeparamref name="T"/> (<see langword="null"/> for a reference type) when there is none.
    /// </returns>
    public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> source, CancellationToken ct = default);

    /// <summary>
    /// Determines whether the query returns any result, without loading it.
    /// </summary>
    /// <typeparam name="T">Type of the query results.</typeparam>
    /// <param name="source">Query to test.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when at least one item matches.</returns>
    public Task<bool> AnyAsync<T>(IQueryable<T> source, CancellationToken ct = default);
}
