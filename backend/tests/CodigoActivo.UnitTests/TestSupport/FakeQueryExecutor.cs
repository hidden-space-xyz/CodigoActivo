using CodigoActivo.Application.Abstractions.Querying;

namespace CodigoActivo.UnitTests.TestSupport;

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

    public Task<IReadOnlyList<T>> ToListAsync<T>(
        IQueryable<T> source,
        CancellationToken ct = default
    )
    {
        return Task.FromResult<IReadOnlyList<T>>([.. source]);
    }

    public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> source, CancellationToken ct = default)
    {
        return Task.FromResult(source.FirstOrDefault());
    }

    public Task<bool> AnyAsync<T>(IQueryable<T> source, CancellationToken ct = default)
    {
        return Task.FromResult(source.Any());
    }
}
