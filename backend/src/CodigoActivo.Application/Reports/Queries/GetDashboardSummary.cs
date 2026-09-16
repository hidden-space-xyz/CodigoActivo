using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Reports.Queries;

/// <summary>
/// Carries the criteria used to retrieve dashboard summary.
/// </summary>
public sealed record GetDashboardSummaryQuery : IQuery<DashboardSummaryResponse>;

/// <summary>
/// Executes the query to retrieve dashboard summary.
/// </summary>
/// <param name="dashboard">Repository used to persist and retrieve dashboard.</param>
/// <param name="cache">Cache used to reuse previously computed results.</param>
public sealed class GetDashboardSummaryQueryHandler(
    IDashboardRepository dashboard,
    HybridCache cache
) : IQueryHandler<GetDashboardSummaryQuery, DashboardSummaryResponse>
{
    /// <summary>
    /// Handles the request to retrieve dashboard summary.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a dashboard summary.</returns>
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
            CacheTags.DashboardSummarySources,
            ct
        );
    }
}
