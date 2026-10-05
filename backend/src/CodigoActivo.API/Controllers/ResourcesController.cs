using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Resources.Commands;
using CodigoActivo.Application.Resources.Contracts;
using CodigoActivo.Application.Resources.Queries;
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
    /// <param name="getById">Query handler that reads the result of the command.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a resource, or an error response.</returns>
    [HttpPost]
    [AllowOnlyAdmin]
    [ProducesResponseType<ResourceResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ResourceResponse>> CreateAsync(
        [FromBody] CreateResourceRequest request,
        [FromServices] CreateResourceCommandHandler handler,
        [FromServices] GetResourceByIdQueryHandler getById,
        CancellationToken ct
    )
    {
        return await ToCreatedAfterAsync(
            await handler.HandleAsync(new CreateResourceCommand(request, UserId), ct),
            id => getById.HandleAsync(new GetResourceByIdQuery(id), ct),
            id => $"/api/resources/{id}"
        );
    }

    /// <summary>
    /// Updates the selected resource with the validated request.
    /// </summary>
    /// <param name="resourceId">Identifier of the resource.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getById">Query handler that reads the result of the command.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a resource, or an error response.</returns>
    [HttpPut("{resourceId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<ResourceResponse>> UpdateAsync(
        Guid resourceId,
        [FromBody] UpdateResourceRequest request,
        [FromServices] UpdateResourceCommandHandler handler,
        [FromServices] GetResourceByIdQueryHandler getById,
        CancellationToken ct
    )
    {
        return await ToOkAfterAsync(
            await handler.HandleAsync(new UpdateResourceCommand(resourceId, request, UserId), ct),
            () => getById.HandleAsync(new GetResourceByIdQuery(resourceId), ct)
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
