using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Infrastructure.Caching;

/// <summary>
/// Serves the result of an <see cref="ICachedQuery"/> from <see cref="HybridCache"/>, running the
/// handler only when the entry is missing. Other queries pass through.
/// </summary>
/// <typeparam name="TQuery">Type of query handled.</typeparam>
/// <typeparam name="TResult">Result type of the query.</typeparam>
/// <param name="inner">Decorated handler.</param>
/// <param name="cache">Application cache.</param>
/// <param name="clock">Clock that tells the current day for the cache key.</param>
public sealed class CachingQueryDecorator<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    HybridCache cache,
    IClock clock
) : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    /// <inheritdoc />
    public async Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default)
    {
        if (query is not ICachedQuery cached)
        {
            return await inner.HandleAsync(query, ct);
        }

        return await cache.GetOrCreateAsync(
            cached.CacheKey(clock.Today),
            (inner, query),
            static async (state, token) => await state.inner.HandleAsync(state.query, token),
            CachePolicies.For(cached.Duration),
            cached.Tags,
            ct
        );
    }
}
