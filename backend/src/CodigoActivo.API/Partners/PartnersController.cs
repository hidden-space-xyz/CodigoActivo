using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.API.Partners.Contracts;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Partners.Commands;
using CodigoActivo.Application.Partners.Contracts;
using CodigoActivo.Application.Partners.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Partners;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace CodigoActivo.API.Partners;

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
        [FromServices] IQueryHandler<ListPartnersQuery, PagedResult<PartnerResponse>> handler,
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
        [FromServices] IQueryHandler<GetPartnerByIdQuery, Result<PartnerResponse>> handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(new GetPartnerByIdQuery(PartnerId.From(partnerId)), ct)
        );
    }

    /// <summary>
    /// Creates a partner from the validated request.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getById">Query handler that reads the result of the command.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a partner, or an error response.</returns>
    [HttpPost]
    [AllowOnlyAdmin]
    [ProducesResponseType<PartnerResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PartnerResponse>> CreateAsync(
        [FromBody] CreatePartnerRequest request,
        [FromServices] ICommandHandler<CreatePartnerCommand, Result<PartnerId>> handler,
        [FromServices] IQueryHandler<GetPartnerByIdQuery, Result<PartnerResponse>> getById,
        CancellationToken ct
    )
    {
        return await ToCreatedAfterAsync(
            await handler.HandleAsync(request.ToCommand(), ct),
            id => getById.HandleAsync(new GetPartnerByIdQuery(id), ct),
            id => $"/api/partners/{id}"
        );
    }

    /// <summary>
    /// Updates the selected partner with the validated request.
    /// </summary>
    /// <param name="partnerId">Identifier of the partner.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getById">Query handler that reads the result of the command.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a partner, or an error response.</returns>
    [HttpPut("{partnerId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<PartnerResponse>> UpdateAsync(
        Guid partnerId,
        [FromBody] UpdatePartnerRequest request,
        [FromServices] ICommandHandler<UpdatePartnerCommand, Result> handler,
        [FromServices] IQueryHandler<GetPartnerByIdQuery, Result<PartnerResponse>> getById,
        CancellationToken ct
    )
    {
        return await ToOkAfterAsync(
            await handler.HandleAsync(request.ToCommand(PartnerId.From(partnerId)), ct),
            () => getById.HandleAsync(new GetPartnerByIdQuery(PartnerId.From(partnerId)), ct)
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
        [FromServices] ICommandHandler<DeletePartnerCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(
            await handler.HandleAsync(new DeletePartnerCommand(PartnerId.From(partnerId)), ct)
        );
    }
}
