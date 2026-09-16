using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Partners.Commands;
using CodigoActivo.Application.Partners.Queries;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace CodigoActivo.API.Controllers;

/// <summary>
/// Exposes HTTP endpoints for querying and managing partners.
/// </summary>
[ApiController]
[Route("api/partners")]
public class PartnersController : ApiControllerBase
{
    /// <summary>
    /// Lists the partners that match the supplied filters.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a paged partner, or an error response.</returns>
    [HttpGet]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Partners)]
    public async Task<ActionResult<PagedResult<PartnerResponse>>> ListAsync(
        [FromQuery] PartnerListQuery query,
        [FromServices] ListPartnersQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListPartnersQuery(query), ct));
    }

    /// <summary>
    /// Gets the requested partner.
    /// </summary>
    /// <param name="partnerId">Identifier of the partner.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a partner, or an error response.</returns>
    [HttpGet("{partnerId:guid}")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Partners)]
    public async Task<ActionResult<PartnerResponse>> GetAsync(
        Guid partnerId,
        [FromServices] GetPartnerByIdQueryHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetPartnerByIdQuery(partnerId), ct));
    }

    /// <summary>
    /// Creates a partner from the validated request.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a partner, or an error response.</returns>
    [HttpPost]
    [AllowOnlyAdmin]
    public async Task<ActionResult<PartnerResponse>> CreateAsync(
        [FromBody] CreatePartnerRequest request,
        [FromServices] CreatePartnerCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToCreated(
            await handler.HandleAsync(new CreatePartnerCommand(request, UserId), ct),
            p => $"/api/partners/{p.Id}"
        );
    }

    /// <summary>
    /// Updates the selected partner with the validated request.
    /// </summary>
    /// <param name="partnerId">Identifier of the partner.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a partner, or an error response.</returns>
    [HttpPut("{partnerId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<PartnerResponse>> UpdateAsync(
        Guid partnerId,
        [FromBody] UpdatePartnerRequest request,
        [FromServices] UpdatePartnerCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(new UpdatePartnerCommand(partnerId, request, UserId), ct)
        );
    }

    /// <summary>
    /// Deletes the selected partner.
    /// </summary>
    /// <param name="partnerId">Identifier of the partner.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpDelete("{partnerId:guid}")]
    [AllowOnlyAdmin]
    public async Task<IActionResult> DeleteAsync(
        Guid partnerId,
        [FromServices] DeletePartnerCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToNoContent(await handler.HandleAsync(new DeletePartnerCommand(partnerId), ct));
    }
}
