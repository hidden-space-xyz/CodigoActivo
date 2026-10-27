using CodigoActivo.Application.Abstractions.Querying;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database;

/// <summary>
/// Executes read-only Entity Framework queries and materializes their results.
/// </summary>
public sealed class QueryExecutor : IQueryExecutor
{
    /// <summary>
    /// Counts the query and loads one page of it in its own order; a page past the end loads no item
    /// and still reports the total.
    /// </summary>
    /// <typeparam name="T">Type of item processed by the operation.</typeparam>
    /// <param name="source">Query to page, in the order the pages follow.</param>
    /// <param name="page">One-based page number to return.</param>
    /// <param name="pageSize">Maximum number of items in the page.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the items of the page, the total count and the page asked for.</returns>
    public async Task<PagedResult<T>> ToPagedAsync<T>(
        IQueryable<T> source,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var total = await source.CountAsync(ct);
        var skip = (page - 1L) * pageSize;
        var items =
            skip >= total
                ? []
                : await source.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<T>(items, total, page, pageSize);
    }

    /// <summary>
    /// Runs the query and loads every result.
    /// </summary>
    /// <typeparam name="T">Type of item processed by the operation.</typeparam>
    /// <param name="source">Source sequence to query.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains every result, in the order of the query.</returns>
    public async Task<IReadOnlyList<T>> ToListAsync<T>(
        IQueryable<T> source,
        CancellationToken ct = default
    )
    {
        return await source.ToListAsync(ct);
    }

    /// <summary>
    /// Returns the first matching query result, or <see langword="null"/> when none exists.
    /// </summary>
    /// <typeparam name="T">Type of item processed by the operation.</typeparam>
    /// <param name="source">Source sequence to query.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the first result, or <see langword="null"/> when there is none.</returns>
    public async Task<T?> FirstOrDefaultAsync<T>(
        IQueryable<T> source,
        CancellationToken ct = default
    )
    {
        return await source.FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Determines whether the query returns any result.
    /// </summary>
    /// <typeparam name="T">Type of item processed by the operation.</typeparam>
    /// <param name="source">Source sequence to query.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when at least one item matches.</returns>
    public async Task<bool> AnyAsync<T>(IQueryable<T> source, CancellationToken ct = default)
    {
        return await source.AnyAsync(ct);
    }
}
