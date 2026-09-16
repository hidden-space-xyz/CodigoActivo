using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Resources.Commands;
using CodigoActivo.Application.Resources.Queries;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace CodigoActivo.API.Controllers;

/// <summary>
/// Exposes HTTP endpoints for querying and managing resources.
/// </summary>
[ApiController]
[Route("api/resources")]
public class ResourcesController : ApiControllerBase
{
    /// <summary>
    /// Lists the resources that match the supplied filters.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a paged resource list item, or an error response.</returns>
    [HttpGet]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Resources)]
    public async Task<ActionResult<PagedResult<ResourceListItemResponse>>> ListAsync(
        [FromQuery] ResourceListQuery query,
        [FromServices] ListResourcesQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListResourcesQuery(query), ct));
    }

    /// <summary>
    /// Executes the types endpoint for resources.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a resource type, or an error response.</returns>
    [HttpGet("types")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<IReadOnlyList<ResourceTypeResponse>>> TypesAsync(
        [FromServices] ListResourceTypesQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListResourceTypesQuery(), ct));
    }

    /// <summary>
    /// Gets the requested resource.
    /// </summary>
    /// <param name="resourceId">Identifier of the resource.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a resource, or an error response.</returns>
    [HttpGet("{resourceId:guid}")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Resources)]
    public async Task<ActionResult<ResourceResponse>> GetAsync(
        Guid resourceId,
        [FromServices] GetResourceByIdQueryHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetResourceByIdQuery(resourceId), ct));
    }

    /// <summary>
    /// Creates a resource from the validated request.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a resource, or an error response.</returns>
    [HttpPost]
    [AllowOnlyAdmin]
    public async Task<ActionResult<ResourceResponse>> CreateAsync(
        [FromBody] CreateResourceRequest request,
        [FromServices] CreateResourceCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToCreated(
            await handler.HandleAsync(new CreateResourceCommand(request, UserId), ct),
            r => $"/api/resources/{r.Id}"
        );
    }

    /// <summary>
    /// Updates the selected resource with the validated request.
    /// </summary>
    /// <param name="resourceId">Identifier of the resource.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a resource, or an error response.</returns>
    [HttpPut("{resourceId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<ResourceResponse>> UpdateAsync(
        Guid resourceId,
        [FromBody] UpdateResourceRequest request,
        [FromServices] UpdateResourceCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(new UpdateResourceCommand(resourceId, request, UserId), ct)
        );
    }

    /// <summary>
    /// Deletes the selected resource.
    /// </summary>
    /// <param name="resourceId">Identifier of the resource.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpDelete("{resourceId:guid}")]
    [AllowOnlyAdmin]
    public async Task<IActionResult> DeleteAsync(
        Guid resourceId,
        [FromServices] DeleteResourceCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToNoContent(await handler.HandleAsync(new DeleteResourceCommand(resourceId), ct));
    }
}
