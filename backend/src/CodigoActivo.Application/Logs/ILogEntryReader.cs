using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Logs.Contracts;

namespace CodigoActivo.Application.Logs;

/// <summary>
/// Read port of the diagnostic and audit log tables, which the read store does not expose: plain
/// filters in Infrastructure, most of them over an index (the category and person searches and the
/// actor type, entity type and outcome filters have none), with the names of the people and things
/// the audit entries mention joined in.
/// </summary>
public interface ILogEntryReader
{
    /// <summary>
    /// Lists the diagnostic log entries that match the criteria, without their exceptions.
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
    /// Lists, oldest first, every entry the audit log still holds that the account ran signed in or
    /// that names the account or one of the given people as its main or related entity, such as a
    /// login, which runs before there is a session, or an administrator's change.
    /// </summary>
    /// <param name="userId">Identifier of the account.</param>
    /// <param name="subjectIds">The account and the people in its care.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the entries.</returns>
    public Task<IReadOnlyList<AccountAuditEntryResponse>> ListAccountAuditAsync(
        Guid userId,
        IReadOnlyCollection<Guid> subjectIds,
        CancellationToken ct = default
    );
}
