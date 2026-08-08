using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Announcements.Queries;

public sealed record GetAnnouncementYearsQuery : IQuery<IReadOnlyList<int>>;

public sealed class GetAnnouncementYearsQueryHandler(
    IAnnouncementRepository announcements,
    IQueryExecutor executor,
    HybridCache cache
) : IQueryHandler<GetAnnouncementYearsQuery, IReadOnlyList<int>>
{
    public async Task<IReadOnlyList<int>> HandleAsync(
        GetAnnouncementYearsQuery query,
        CancellationToken ct = default
    )
    {
        return await cache.GetOrCreateAsync(
            "announcements:years",
            token => new ValueTask<IReadOnlyList<int>>(
                executor.ToListAsync(
                    announcements
                        .Query()
                        .Select(a => a.CreatedAt.Year)
                        .Distinct()
                        .OrderByDescending(year => year),
                    token
                )
            ),
            CachePolicies.PublicContent,
            [CacheTags.Announcements],
            ct
        );
    }
}
