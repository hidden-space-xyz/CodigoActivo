namespace CodigoActivo.Application.BlockedData;

/// <summary>
/// Ends the retention of the data history: the previous states of changed and deleted rows are
/// blocked (article 32 LOPDGDD) only while liabilities from their processing can still be claimed.
/// </summary>
public interface IDataHistoryRetention
{
    /// <summary>
    /// Physically removes every entry whose retention has ended. It runs immediately, without
    /// waiting for the unit of work.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the number of entries removed.</returns>
    public Task<int> PurgeExpiredAsync(DateTimeOffset now, CancellationToken ct = default);
}
