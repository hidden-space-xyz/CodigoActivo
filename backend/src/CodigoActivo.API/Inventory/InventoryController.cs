using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.API.Inventory.Contracts;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Inventory.Commands;
using CodigoActivo.Application.Inventory.Contracts;
using CodigoActivo.Application.Inventory.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Inventory;
using CodigoActivo.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Inventory;

/// <summary>
/// Exposes HTTP endpoints for the inventory of the association: categories with the columns an
/// administrator defines and the entries that fill them.
/// </summary>
[ApiController]
[Route("api/inventory/categories")]
[AllowOnlyStaff(StaffRole.Administrator)]
public class InventoryController : ApiControllerBase
{
    /// <summary>
    /// Lists the inventory categories that match the supplied filters, with their totals.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a page of categories.</returns>
    [HttpGet]
    public async Task<ActionResult<PagedResult<InventoryCategorySummaryResponse>>> CategoriesAsync(
        [FromQuery] InventoryCategoryListQuery query,
        [FromServices]
            IQueryHandler<
            ListInventoryCategoriesQuery,
            PagedResult<InventoryCategorySummaryResponse>
        > handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListInventoryCategoriesQuery(query), ct));
    }

    /// <summary>
    /// Gets an inventory category with its columns.
    /// </summary>
    /// <param name="categoryId">Identifier of the category.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the category, or an error response.</returns>
    [HttpGet("{categoryId:guid}")]
    public async Task<ActionResult<InventoryCategoryResponse>> CategoryAsync(
        Guid categoryId,
        [FromServices]
            IQueryHandler<GetInventoryCategoryByIdQuery, Result<InventoryCategoryResponse>> handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(
                new GetInventoryCategoryByIdQuery(InventoryCategoryId.From(categoryId)),
                ct
            )
        );
    }

    /// <summary>
    /// Creates an inventory category with its columns.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getById">Query handler that reads the created category.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A 201 response with the category and its location, or an error response.</returns>
    [HttpPost]
    [ProducesResponseType<InventoryCategoryResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<InventoryCategoryResponse>> CreateCategoryAsync(
        [FromBody] CreateInventoryCategoryRequest request,
        [FromServices]
            ICommandHandler<CreateInventoryCategoryCommand, Result<InventoryCategoryId>> handler,
        [FromServices]
            IQueryHandler<GetInventoryCategoryByIdQuery, Result<InventoryCategoryResponse>> getById,
        CancellationToken ct
    )
    {
        return await ToCreatedAfterAsync(
            await handler.HandleAsync(request.ToCommand(), ct),
            id => getById.HandleAsync(new GetInventoryCategoryByIdQuery(id), ct),
            id => $"/api/inventory/categories/{id}"
        );
    }

    /// <summary>
    /// Renames an inventory category and replaces its columns; a column left out is removed with
    /// its values.
    /// </summary>
    /// <param name="categoryId">Identifier of the category.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getById">Query handler that reads the updated category.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the category, or an error response.</returns>
    [HttpPut("{categoryId:guid}")]
    public async Task<ActionResult<InventoryCategoryResponse>> UpdateCategoryAsync(
        Guid categoryId,
        [FromBody] UpdateInventoryCategoryRequest request,
        [FromServices] ICommandHandler<UpdateInventoryCategoryCommand, Result> handler,
        [FromServices]
            IQueryHandler<GetInventoryCategoryByIdQuery, Result<InventoryCategoryResponse>> getById,
        CancellationToken ct
    )
    {
        var id = InventoryCategoryId.From(categoryId);
        return await ToOkAfterAsync(
            await handler.HandleAsync(request.ToCommand(id), ct),
            () => getById.HandleAsync(new GetInventoryCategoryByIdQuery(id), ct)
        );
    }

    /// <summary>
    /// Deletes an inventory category with its entries.
    /// </summary>
    /// <param name="categoryId">Identifier of the category.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A 204 response, or an error response.</returns>
    [HttpDelete("{categoryId:guid}")]
    public async Task<IActionResult> DeleteCategoryAsync(
        Guid categoryId,
        [FromServices] ICommandHandler<DeleteInventoryCategoryCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(
            await handler.HandleAsync(
                new DeleteInventoryCategoryCommand(InventoryCategoryId.From(categoryId)),
                ct
            )
        );
    }

    /// <summary>
    /// Lists every entry of an inventory category.
    /// </summary>
    /// <param name="categoryId">Identifier of the category.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the entries, or an error response.</returns>
    [HttpGet("{categoryId:guid}/entries")]
    public async Task<ActionResult<IReadOnlyList<InventoryEntryResponse>>> EntriesAsync(
        Guid categoryId,
        [FromServices]
            IQueryHandler<
            ListInventoryEntriesQuery,
            Result<IReadOnlyList<InventoryEntryResponse>>
        > handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(
                new ListInventoryEntriesQuery(InventoryCategoryId.From(categoryId)),
                ct
            )
        );
    }

    /// <summary>
    /// Adds an entry to an inventory category.
    /// </summary>
    /// <param name="categoryId">Identifier of the category.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getById">Query handler that reads the added entry.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A 201 response with the entry and its location, or an error response.</returns>
    [HttpPost("{categoryId:guid}/entries")]
    [ProducesResponseType<InventoryEntryResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<InventoryEntryResponse>> AddEntryAsync(
        Guid categoryId,
        [FromBody] AddInventoryEntryRequest request,
        [FromServices] ICommandHandler<AddInventoryEntryCommand, Result<InventoryEntryId>> handler,
        [FromServices]
            IQueryHandler<GetInventoryEntryByIdQuery, Result<InventoryEntryResponse>> getById,
        CancellationToken ct
    )
    {
        var id = InventoryCategoryId.From(categoryId);
        return await ToCreatedAfterAsync(
            await handler.HandleAsync(request.ToCommand(id), ct),
            entryId => getById.HandleAsync(new GetInventoryEntryByIdQuery(id, entryId), ct),
            entryId => $"/api/inventory/categories/{categoryId}/entries/{entryId}"
        );
    }

    /// <summary>
    /// Replaces the values of an entry of an inventory category.
    /// </summary>
    /// <param name="categoryId">Identifier of the category.</param>
    /// <param name="entryId">Identifier of the entry.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getById">Query handler that reads the updated entry.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the entry, or an error response.</returns>
    [HttpPut("{categoryId:guid}/entries/{entryId:guid}")]
    public async Task<ActionResult<InventoryEntryResponse>> UpdateEntryAsync(
        Guid categoryId,
        Guid entryId,
        [FromBody] UpdateInventoryEntryRequest request,
        [FromServices] ICommandHandler<UpdateInventoryEntryCommand, Result> handler,
        [FromServices]
            IQueryHandler<GetInventoryEntryByIdQuery, Result<InventoryEntryResponse>> getById,
        CancellationToken ct
    )
    {
        var id = InventoryCategoryId.From(categoryId);
        var entry = InventoryEntryId.From(entryId);
        return await ToOkAfterAsync(
            await handler.HandleAsync(request.ToCommand(id, entry), ct),
            () => getById.HandleAsync(new GetInventoryEntryByIdQuery(id, entry), ct)
        );
    }

    /// <summary>
    /// Deletes an entry of an inventory category.
    /// </summary>
    /// <param name="categoryId">Identifier of the category.</param>
    /// <param name="entryId">Identifier of the entry.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A 204 response, or an error response.</returns>
    [HttpDelete("{categoryId:guid}/entries/{entryId:guid}")]
    public async Task<IActionResult> DeleteEntryAsync(
        Guid categoryId,
        Guid entryId,
        [FromServices] ICommandHandler<DeleteInventoryEntryCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(
            await handler.HandleAsync(
                new DeleteInventoryEntryCommand(
                    InventoryCategoryId.From(categoryId),
                    InventoryEntryId.From(entryId)
                ),
                ct
            )
        );
    }
}
