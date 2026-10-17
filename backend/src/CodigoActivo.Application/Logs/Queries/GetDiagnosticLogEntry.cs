using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Logs.Contracts;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Logs.Queries;

/// <summary>
/// Carries the criteria used to retrieve one diagnostic log entry.
/// </summary>
/// <param name="Id">Identifier of the entry.</param>
public sealed record GetDiagnosticLogEntryQuery(Guid Id)
    : IQuery<Result<DiagnosticLogEntryDetailResponse>>;

/// <summary>
/// Executes the query to retrieve one diagnostic log entry.
/// </summary>
/// <param name="reader">Reader of the log tables.</param>
public sealed class GetDiagnosticLogEntryQueryHandler(ILogEntryReader reader)
    : IQueryHandler<GetDiagnosticLogEntryQuery, Result<DiagnosticLogEntryDetailResponse>>
{
    /// <summary>
    /// Handles the request to retrieve one diagnostic log entry.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the entry, or <see cref="ApplicationErrorCode.DiagnosticLogEntryNotFound"/>.
    /// </returns>
    public async Task<Result<DiagnosticLogEntryDetailResponse>> HandleAsync(
        GetDiagnosticLogEntryQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        var entry = await reader.FindDiagnosticAsync(query.Id, ct);
        return entry is null
            ? Error.NotFound(ApplicationErrorCode.DiagnosticLogEntryNotFound)
            : entry;
    }
}
