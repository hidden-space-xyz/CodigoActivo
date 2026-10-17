using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.API.Security;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Logs.Contracts;
using CodigoActivo.Application.Logs.Queries;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CodigoActivo.API.Logs;

/// <summary>
/// Exposes the diagnostic and audit logs to administrators.
/// </summary>
[ApiController]
[Route("api/logs")]
[AllowOnlyAdmin]
[EnableRateLimiting(SecurityPolicies.Reports)]
public class LogsController : ApiControllerBase
{
    /// <summary>
    /// Lists the diagnostic log entries that match the supplied filters.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a page of diagnostic log entries.</returns>
    [HttpGet("diagnostics")]
    public async Task<ActionResult<PagedResult<DiagnosticLogEntryResponse>>> ListDiagnosticsAsync(
        [FromQuery] DiagnosticLogEntryListQuery query,
        [FromServices]
            IQueryHandler<
            ListDiagnosticLogEntriesQuery,
            PagedResult<DiagnosticLogEntryResponse>
        > handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListDiagnosticLogEntriesQuery(query), ct));
    }

    /// <summary>
    /// Gets one diagnostic log entry with its exception.
    /// </summary>
    /// <param name="id">Identifier of the entry.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the entry, or an error response.</returns>
    [HttpGet("diagnostics/{id:guid}")]
    public async Task<ActionResult<DiagnosticLogEntryDetailResponse>> GetDiagnosticAsync(
        Guid id,
        [FromServices]
            IQueryHandler<
            GetDiagnosticLogEntryQuery,
            Result<DiagnosticLogEntryDetailResponse>
        > handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetDiagnosticLogEntryQuery(id), ct));
    }

    /// <summary>
    /// Lists the audit log entries that match the supplied filters.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a page of audit log entries.</returns>
    [HttpGet("audit")]
    public async Task<ActionResult<PagedResult<AuditLogEntryResponse>>> ListAuditAsync(
        [FromQuery] AuditLogEntryListQuery query,
        [FromServices]
            IQueryHandler<ListAuditLogEntriesQuery, PagedResult<AuditLogEntryResponse>> handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListAuditLogEntriesQuery(query), ct));
    }
}
