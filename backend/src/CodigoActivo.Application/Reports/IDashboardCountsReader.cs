namespace CodigoActivo.Application.Reports;

/// <summary>
/// Read port of the dashboard aggregates that the read store cannot express: plain SQL in
/// Infrastructure that counts whole tables in one round trip, so the dashboard never loads them.
/// </summary>
public interface IDashboardCountsReader
{
    /// <summary>
    /// Gets the number of rows of each table shown on the dashboard summary.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a dashboard counts.</returns>
    public Task<DashboardCounts> GetCountsAsync(CancellationToken ct = default);

    /// <summary>
    /// Counts the rows of each dashboard source by the interval of its creation time.
    /// </summary>
    /// <param name="thresholds">Instants that separate the intervals, in ascending order.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the non-empty intervals of every source.</returns>
    public Task<DashboardCreatedCounts> CountCreatedAsync(
        IReadOnlyList<DateTimeOffset> thresholds,
        CancellationToken ct = default
    );
}
