using CodigoActivo.Domain.Inventory;

namespace CodigoActivo.Application.Inventory.Commands;

/// <summary>
/// Carries the input required to add an entry to an inventory category.
/// </summary>
/// <param name="CategoryId">Identifier of the category.</param>
/// <param name="Quantity">Value of the quantity column, or <see langword="null"/> for none.</param>
/// <param name="Values">
/// Value of each other column as written by a client, keyed by column; a blank or missing value
/// leaves the column empty.
/// </param>
public sealed record AddInventoryEntryCommand(
    InventoryCategoryId CategoryId,
    int? Quantity,
    [property: Required, MaxLength(InventoryCategory.MaxColumns)]
        IReadOnlyDictionary<InventoryColumnId, string> Values
) : ICommand<Result<InventoryEntryId>>, IRoutineCommand;

/// <summary>
/// Executes the command to add an entry to an inventory category.
/// </summary>
/// <param name="categories">Repository used to persist and retrieve inventory categories.</param>
public sealed class AddInventoryEntryCommandHandler(IInventoryCategoryRepository categories)
    : ICommandHandler<AddInventoryEntryCommand, Result<InventoryEntryId>>
{
    /// <summary>
    /// Handles the request to add an inventory entry.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the identifier of the new entry;
    /// <see cref="ApplicationErrorCode.InventoryCategoryNotFound"/>; or a failure of
    /// <see cref="InventoryCategory.AddEntry"/>.
    /// </returns>
    public async Task<Result<InventoryEntryId>> HandleAsync(
        AddInventoryEntryCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await categories.GetByIdAsync(command.CategoryId, ct);
        if (category is null)
        {
            return Error.NotFound(ApplicationErrorCode.InventoryCategoryNotFound);
        }

        return category.AddEntry(command.Quantity, command.Values);
    }
}
