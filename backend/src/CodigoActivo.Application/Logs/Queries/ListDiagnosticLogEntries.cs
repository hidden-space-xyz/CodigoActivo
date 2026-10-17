using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Logs.Contracts;

namespace CodigoActivo.Application.Logs.Queries;

/// <summary>
/// Carries the criteria used to list diagnostic log entries.
/// </summary>
/// <param name="Filters">Filtering, sorting, and paging criteria supplied by the client.</param>
public sealed record ListDiagnosticLogEntriesQuery(DiagnosticLogEntryListQuery Filters)
    : IQuery<PagedResult<DiagnosticLogEntryResponse>>;

/// <summary>
/// Executes the query to list diagnostic log entries.
/// </summary>
/// <param name="reader">Reader of the log tables.</param>
/// <param name="clock">Clock whose time zone the day filters are read in.</param>
public sealed class ListDiagnosticLogEntriesQueryHandler(ILogEntryReader reader, IClock clock)
    : IQueryHandler<ListDiagnosticLogEntriesQuery, PagedResult<DiagnosticLogEntryResponse>>
{
    /// <summary>
    /// Handles the request to list diagnostic log entries.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the page of entries.</returns>
    public Task<PagedResult<DiagnosticLogEntryResponse>> HandleAsync(
        ListDiagnosticLogEntriesQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        var filters = query.Filters;
        return reader.ListDiagnosticsAsync(
            filters,
            LogPeriod.Of(filters.OccurredFrom, filters.OccurredTo, clock.TimeZone),
            ct
        );
    }
}
