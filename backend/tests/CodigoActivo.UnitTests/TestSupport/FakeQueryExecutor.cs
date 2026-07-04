using CodigoActivo.Domain.Common;

namespace CodigoActivo.UnitTests.TestSupport;

/// <summary>
/// Synchronous <see cref="IQueryExecutor"/> for unit tests. Mirrors the production
/// <c>QueryExecutor</c> exactly (same paging maths) but materializes with LINQ-to-Objects, so a
/// service composing a query over <c>list.AsQueryable()</c> exercises its real projection, sort and
/// search expressions without an EF Core provider or the async query pipeline.
/// </summary>
public sealed class FakeQueryExecutor : IQueryExecutor
{
    public Task<PagedResult<T>> ToPagedAsync<T>(
        IQueryable<T> source,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var total = source.Count();
        var items = source.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(new PagedResult<T>(items, total, page, pageSize));
    }

    public Task<IReadOnlyList<T>> ToListAsync<T>(IQueryable<T> source, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<T>>(source.ToList());
    }

    public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> source, CancellationToken ct = default)
    {
        return Task.FromResult(source.FirstOrDefault());
    }
}
