using CodigoActivo.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database;

/// <summary>
/// Executes read-only Entity Framework queries and materializes their results.
/// </summary>
public sealed class QueryExecutor : IQueryExecutor
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
    /// Converts the value to list.
    /// </summary>
    /// <typeparam name="T">Type of item processed by the operation.</typeparam>
    /// <param name="source">Source sequence to query.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching t items.</returns>
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
    /// <returns>A task whose result contains the matching t, or <see langword="null"/> when it is not found.</returns>
    public async Task<T?> FirstOrDefaultAsync<T>(
        IQueryable<T> source,
        CancellationToken ct = default
    )
    {
        return await source.FirstOrDefaultAsync(ct);
    }
}
