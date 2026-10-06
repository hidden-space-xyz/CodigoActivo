using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Common.Caching;

/// <summary>
/// Evicts the cached data that the committed changes made stale: once per commit, with the tags
/// of every committed event, before the listeners of each event run.
/// </summary>
/// <param name="invalidator">Invalidator of the application and HTTP output caches.</param>
public sealed class CacheInvalidationOnCommit(ICacheInvalidator invalidator)
    : ICommittedEventsHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);
        var tags = domainEvents.SelectMany(CacheTagsByEvent.For).Distinct().ToList();
        if (tags.Count > 0)
        {
            await invalidator.InvalidateAsync(tags);
        }
    }
}
