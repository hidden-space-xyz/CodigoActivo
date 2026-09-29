namespace CodigoActivo.Application.Reports;

/// <summary>
/// Reads the totals shown on the dashboard in a single round trip.
/// </summary>
public interface IDashboardCountsReader
{
    /// <summary>
    /// Gets the requested counts.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a dashboard counts.</returns>
    public Task<DashboardCounts> GetCountsAsync(CancellationToken ct = default);
}
