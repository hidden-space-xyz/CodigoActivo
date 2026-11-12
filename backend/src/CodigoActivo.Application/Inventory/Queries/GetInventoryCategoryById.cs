using CodigoActivo.Application.Inventory.Contracts;
using CodigoActivo.Domain.Inventory;

namespace CodigoActivo.Application.Inventory.Queries;

/// <summary>
/// Carries the criteria used to retrieve an inventory category by identifier.
/// </summary>
/// <param name="CategoryId">Identifier of the category.</param>
public sealed record GetInventoryCategoryByIdQuery(InventoryCategoryId CategoryId)
    : IQuery<Result<InventoryCategoryResponse>>;

/// <summary>
/// Executes the query to retrieve an inventory category with its columns.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetInventoryCategoryByIdQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor
) : IQueryHandler<GetInventoryCategoryByIdQuery, Result<InventoryCategoryResponse>>
{
    /// <summary>
    /// Handles the request to retrieve an inventory category by identifier.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the category, or
    /// <see cref="ApplicationErrorCode.InventoryCategoryNotFound"/>.
    /// </returns>
    public async Task<Result<InventoryCategoryResponse>> HandleAsync(
        GetInventoryCategoryByIdQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        var response = await executor.FirstOrDefaultAsync(
            readStore
                .InventoryCategories.Where(category => category.Id == query.CategoryId.Value)
                .Select(InventoryProjections.Category),
            ct
        );
        return response is null
            ? Error.NotFound(ApplicationErrorCode.InventoryCategoryNotFound)
            : response;
    }
}
