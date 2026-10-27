using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.API.EventCategories.Contracts;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.EventCategories.Commands;
using CodigoActivo.Application.EventCategories.Contracts;
using CodigoActivo.Application.EventCategories.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.EventCategories;

/// <summary>
/// Exposes HTTP endpoints for managing the category types events are classified with.
/// </summary>
[ApiController]
[Route("api/events/categoryType")]
[Tags("Events")]
public class EventCategoryTypesController : ApiControllerBase
{
    /// <summary>
    /// Lists the event category types that match the supplied filters.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a page of category types.</returns>
    [HttpGet]
    [AllowOnlyAdmin]
    public async Task<ActionResult<PagedResult<EventCategoryTypeResponse>>> CategoryTypesAsync(
        [FromQuery] EventCategoryTypeListQuery query,
        [FromServices]
            IQueryHandler<
            ListEventCategoryTypesQuery,
            PagedResult<EventCategoryTypeResponse>
        > handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListEventCategoryTypesQuery(query), ct));
    }

    /// <summary>
    /// Creates a category type.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getById">Query handler that reads the created category type.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A 201 response with the category type and its location, or an error response.</returns>
    [HttpPost]
    [AllowOnlyAdmin]
    [ProducesResponseType<EventCategoryTypeResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<EventCategoryTypeResponse>> CreateCategoryTypeAsync(
        [FromBody] CreateEventCategoryTypeRequest request,
        [FromServices]
            ICommandHandler<CreateEventCategoryTypeCommand, Result<EventCategoryTypeId>> handler,
        [FromServices]
            IQueryHandler<GetEventCategoryTypeByIdQuery, Result<EventCategoryTypeResponse>> getById,
        CancellationToken ct
    )
    {
        return await ToCreatedAfterAsync(
            await handler.HandleAsync(request.ToCommand(), ct),
            id => getById.HandleAsync(new GetEventCategoryTypeByIdQuery(id), ct),
            id => $"/api/events/categoryType/{id}"
        );
    }

    /// <summary>
    /// Updates a category type with the supplied data.
    /// </summary>
    /// <param name="eventCategoryTypeId">Identifier of the event category type.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getById">Query handler that reads the updated category type.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the category type, or an error response.</returns>
    [HttpPut("{eventCategoryTypeId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventCategoryTypeResponse>> UpdateCategoryTypeAsync(
        Guid eventCategoryTypeId,
        [FromBody] UpdateEventCategoryTypeRequest request,
        [FromServices] ICommandHandler<UpdateEventCategoryTypeCommand, Result> handler,
        [FromServices]
            IQueryHandler<GetEventCategoryTypeByIdQuery, Result<EventCategoryTypeResponse>> getById,
        CancellationToken ct
    )
    {
        var categoryTypeId = EventCategoryTypeId.From(eventCategoryTypeId);
        return await ToOkAfterAsync(
            await handler.HandleAsync(request.ToCommand(categoryTypeId), ct),
            () => getById.HandleAsync(new GetEventCategoryTypeByIdQuery(categoryTypeId), ct)
        );
    }

    /// <summary>
    /// Deletes a category type, removing it from every event tagged with it; it is refused while it
    /// is the only category of an event.
    /// </summary>
    /// <param name="eventCategoryTypeId">Identifier of the event category type.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A 204 response, or an error response.</returns>
    [HttpDelete("{eventCategoryTypeId:guid}")]
    [AllowOnlyAdmin]
    public async Task<IActionResult> DeleteCategoryTypeAsync(
        Guid eventCategoryTypeId,
        [FromServices] ICommandHandler<DeleteEventCategoryTypeCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(
            await handler.HandleAsync(
                new DeleteEventCategoryTypeCommand(EventCategoryTypeId.From(eventCategoryTypeId)),
                ct
            )
        );
    }
}
