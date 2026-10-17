using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Logs.Contracts;

namespace CodigoActivo.Application.Logs.Queries;

/// <summary>
/// Carries the criteria used to list audit log entries.
/// </summary>
/// <param name="Filters">Filtering, sorting, and paging criteria supplied by the client.</param>
public sealed record ListAuditLogEntriesQuery(AuditLogEntryListQuery Filters)
    : IQuery<PagedResult<AuditLogEntryResponse>>;

/// <summary>
/// Executes the query to list audit log entries.
/// </summary>
/// <param name="reader">Reader of the log tables.</param>
/// <param name="clock">Clock whose time zone the day filters are read in.</param>
public sealed class ListAuditLogEntriesQueryHandler(ILogEntryReader reader, IClock clock)
    : IQueryHandler<ListAuditLogEntriesQuery, PagedResult<AuditLogEntryResponse>>
{
    /// <summary>
    /// Handles the request to list audit log entries.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the page of entries.</returns>
    public Task<PagedResult<AuditLogEntryResponse>> HandleAsync(
        ListAuditLogEntriesQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        var filters = query.Filters;
        return reader.ListAuditAsync(
            filters,
            LogPeriod.Of(filters.OccurredFrom, filters.OccurredTo, clock.TimeZone),
            ct
        );
    }
}
