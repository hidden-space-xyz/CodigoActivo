using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Reports.Queries;

public sealed record GetDashboardSummaryQuery : IQuery<DashboardSummaryResponse>;

public sealed class GetDashboardSummaryQueryHandler(
    IDashboardRepository dashboard,
    HybridCache cache
) : IQueryHandler<GetDashboardSummaryQuery, DashboardSummaryResponse>
{
    public async Task<DashboardSummaryResponse> HandleAsync(
        GetDashboardSummaryQuery query,
        CancellationToken ct = default
    )
    {
        return await cache.GetOrCreateAsync(
            "reports:dashboard",
            async token =>
            {
                var counts = await dashboard.GetCountsAsync(token);
                return new DashboardSummaryResponse(
                    counts.Events,
                    counts.Activities,
                    counts.Resources,
                    counts.Announcements,
                    counts.Partners,
                    counts.Users
                );
            },
            CachePolicies.Dashboard,
            CacheTags.DashboardSources,
            ct
        );
    }
}
