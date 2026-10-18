using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Logs.Contracts;

namespace CodigoActivo.Application.Logs;

/// <summary>
/// Read port of the diagnostic and audit log tables, which the read store does not expose: plain
/// filters over their indexes in Infrastructure, with the names of the users the audit entries
/// mention joined in.
/// </summary>
public interface ILogEntryReader
{
    /// <summary>
    /// Lists the diagnostic log entries that match the criteria.
    /// </summary>
    /// <param name="criteria">Filters, sort and page.</param>
    /// <param name="period">Instants the entries must have occurred between.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the page of entries.</returns>
    public Task<PagedResult<DiagnosticLogEntryResponse>> ListDiagnosticsAsync(
        DiagnosticLogEntryListQuery criteria,
        LogPeriod period,
        CancellationToken ct = default
    );

    /// <summary>
    /// Finds one diagnostic log entry with its exception.
    /// </summary>
    /// <param name="id">Identifier of the entry.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the entry, or null when there is none.</returns>
    public Task<DiagnosticLogEntryDetailResponse?> FindDiagnosticAsync(
        Guid id,
        CancellationToken ct = default
    );

    /// <summary>
    /// Lists the audit log entries that match the criteria.
    /// </summary>
    /// <param name="criteria">Filters, sort and page.</param>
    /// <param name="period">Instants the entries must have occurred between.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the page of entries.</returns>
    public Task<PagedResult<AuditLogEntryResponse>> ListAuditAsync(
        AuditLogEntryListQuery criteria,
        LogPeriod period,
        CancellationToken ct = default
    );

    /// <summary>
    /// Lists every operation the account ran that the audit log still holds, oldest first.
    /// </summary>
    /// <param name="actorId">Identifier of the account.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the operations.</returns>
    public Task<IReadOnlyList<OwnAuditEntryResponse>> ListOwnAuditAsync(
        Guid actorId,
        CancellationToken ct = default
    );
}
