using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Reports.Contracts;

namespace CodigoActivo.Application.Reports.Queries;

/// <summary>
/// Carries the request for the totals of the dashboard summary, cached for a minute or until a
/// change to one of their sources evicts them.
/// </summary>
public sealed record GetDashboardSummaryQuery : IQuery<DashboardSummaryResponse>, ICachedQuery
{
    /// <inheritdoc />
    public CacheDuration Duration => CacheDuration.Dashboard;

    /// <inheritdoc />
    public IReadOnlyCollection<string> Tags => CacheTags.DashboardSummarySources;

    /// <inheritdoc />
    public string CacheKey(DateOnly today)
    {
        return "reports:dashboard";
    }
}

/// <summary>
/// Executes the query for the totals of the dashboard summary.
/// </summary>
/// <param name="dashboard">Reader of the dashboard totals.</param>
public sealed class GetDashboardSummaryQueryHandler(IDashboardCountsReader dashboard)
    : IQueryHandler<GetDashboardSummaryQuery, DashboardSummaryResponse>
{
    /// <summary>
    /// Handles the request for the totals of the dashboard summary.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the total of each table.</returns>
    public async Task<DashboardSummaryResponse> HandleAsync(
        GetDashboardSummaryQuery query,
        CancellationToken ct = default
    )
    {
        var counts = await dashboard.GetCountsAsync(ct);
        return new DashboardSummaryResponse(
            counts.Events,
            counts.Activities,
            counts.Resources,
            counts.News,
            counts.Partners,
            counts.Users
        );
    }
}
