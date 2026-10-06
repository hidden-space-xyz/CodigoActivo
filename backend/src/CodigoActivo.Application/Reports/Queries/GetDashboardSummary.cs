using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Reports.Contracts;

namespace CodigoActivo.Application.Reports.Queries;

/// <summary>
/// Carries the criteria used to retrieve dashboard summary.
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
/// Executes the query to retrieve dashboard summary.
/// </summary>
/// <param name="dashboard">Reader of the dashboard totals.</param>
public sealed class GetDashboardSummaryQueryHandler(IDashboardCountsReader dashboard)
    : IQueryHandler<GetDashboardSummaryQuery, DashboardSummaryResponse>
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
