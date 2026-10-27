using CodigoActivo.API.Attributes;
using CodigoActivo.API.BlockedData.Contracts;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.API.Security;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.BlockedData;
using CodigoActivo.Application.BlockedData.Commands;
using CodigoActivo.Application.BlockedData.Contracts;
using CodigoActivo.Application.BlockedData.Queries;
using CodigoActivo.Domain.BlockedData;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CodigoActivo.API.BlockedData;

/// <summary>
/// Gives the administrators the list of consultations of the blocked data, and the account
/// designated for data protection the consultations themselves: opening one for a request,
/// looking accounts up and taking extracts to hand over.
/// </summary>
[ApiController]
[Route("api/blocked-data")]
[Tags("BlockedData")]
[AllowOnlyAdmin]
public class BlockedDataController : ApiControllerBase
{
    /// <summary>
    /// Tells the signed-in administrator whether they may consult the blocked data.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing what the account may do.</returns>
    [HttpGet("access")]
    public async Task<ActionResult<BlockedDataAccessResponse>> GetAccessAsync(
        [FromServices] IQueryHandler<GetBlockedDataAccessQuery, BlockedDataAccessResponse> handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new GetBlockedDataAccessQuery(), ct));
    }

    /// <summary>
    /// Lists the consultations of the blocked data, without any of their data.
    /// </summary>
    /// <param name="query">Paging and sorting.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a page of consultations.</returns>
    [HttpGet("consultations")]
    [EnableRateLimiting(SecurityPolicies.Reports)]
    public async Task<
        ActionResult<PagedResult<BlockedDataConsultationResponse>>
    > ListConsultationsAsync(
        [FromQuery] BlockedDataConsultationListQuery query,
        [FromServices]
            IQueryHandler<
            ListBlockedDataConsultationsQuery,
            PagedResult<BlockedDataConsultationResponse>
        > handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListBlockedDataConsultationsQuery(query), ct));
    }

    /// <summary>
    /// Emails the one-time code that confirms opening a consultation of the blocked data to the
    /// designated account, after its current password. An account whose second factor is an
    /// authenticator application reads its code from it instead.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A 204 response, or an error response.</returns>
    [HttpPost("consultations/code")]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<ActionResult> RequestConsultationCodeAsync(
        [FromBody] BlockedDataConsultationCodeRequest request,
        [FromServices] ICommandHandler<RequestBlockedDataConsultationCodeCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(await handler.HandleAsync(request.ToCommand(), ct));
    }

    /// <summary>
    /// Opens a consultation of the blocked data for a request, after the password and a code of
    /// the second factor of the designated account; the opening is announced to the legal contact.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getById">Query handler that reads the opened consultation.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A 201 response with the consultation and its location, or an error response.</returns>
    [HttpPost("consultations")]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    [ProducesResponseType<BlockedDataConsultationResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<BlockedDataConsultationResponse>> OpenConsultationAsync(
        [FromBody] OpenBlockedDataConsultationRequest request,
        [FromServices]
            ICommandHandler<
            OpenBlockedDataConsultationCommand,
            Result<BlockedDataConsultationId>
        > handler,
        [FromServices]
            IQueryHandler<
            GetBlockedDataConsultationQuery,
            Result<BlockedDataConsultationResponse>
        > getById,
        CancellationToken ct
    )
    {
        return await ToCreatedAfterAsync(
            await handler.HandleAsync(request.ToCommand(), ct),
            id => getById.HandleAsync(new GetBlockedDataConsultationQuery(id), ct),
            id => $"/api/blocked-data/consultations/{id}"
        );
    }

    /// <summary>
    /// Looks accounts up, existing and erased, in an open consultation of the blocked data and
    /// records those found, never the text typed, in the consultation.
    /// </summary>
    /// <param name="consultationId">Identifier of the consultation.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the accounts found, or an error response.</returns>
    [HttpPost("consultations/{consultationId:guid}/searches")]
    [EnableRateLimiting(SecurityPolicies.Reports)]
    public async Task<ActionResult<BlockedDataSearch>> SearchAsync(
        Guid consultationId,
        [FromBody] SearchBlockedDataRequest request,
        [FromServices] ICommandHandler<SearchBlockedDataCommand, Result<BlockedDataSearch>> handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(request.ToCommand(consultationId), ct));
    }

    /// <summary>
    /// Takes an extract of the blocked data about an account an open consultation found and its
    /// minors, and records the extract in the consultation.
    /// </summary>
    /// <param name="consultationId">Identifier of the consultation.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the extract, or an error response.</returns>
    [HttpPost("consultations/{consultationId:guid}/extracts")]
    [EnableRateLimiting(SecurityPolicies.Reports)]
    public async Task<ActionResult<BlockedDataExport>> ExportAsync(
        Guid consultationId,
        [FromBody] ExportBlockedDataRequest request,
        [FromServices] ICommandHandler<ExportBlockedDataCommand, Result<BlockedDataExport>> handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(request.ToCommand(consultationId), ct));
    }
}
