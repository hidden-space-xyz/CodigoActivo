using CodigoActivo.Domain.Inventory;

namespace CodigoActivo.Application.Inventory.Commands;

/// <summary>
/// Carries the input required to replace the values of an inventory entry.
/// </summary>
/// <param name="CategoryId">Identifier of the category.</param>
/// <param name="EntryId">Identifier of the entry.</param>
/// <param name="Quantity">Value of the quantity column, or <see langword="null"/> for none.</param>
/// <param name="Values">
/// Value of each other column as written by a client, keyed by column; a blank or missing value
/// leaves the column empty.
/// </param>
public sealed record UpdateInventoryEntryCommand(
    InventoryCategoryId CategoryId,
    InventoryEntryId EntryId,
    int? Quantity,
    [property: Required, MaxLength(InventoryCategory.MaxColumns)]
        IReadOnlyDictionary<InventoryColumnId, string> Values
) : ICommand<Result>, IRoutineCommand;

/// <summary>
/// Executes the command to replace the values of an inventory entry.
/// </summary>
/// <param name="categories">Repository used to persist and retrieve inventory categories.</param>
public sealed class UpdateInventoryEntryCommandHandler(IInventoryCategoryRepository categories)
    : ICommandHandler<UpdateInventoryEntryCommand, Result>
{
    /// <summary>
    /// Handles the request to update an inventory entry.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result reports success;
    /// <see cref="ApplicationErrorCode.InventoryCategoryNotFound"/>; or a failure of
    /// <see cref="InventoryCategory.UpdateEntry"/>.
    /// </returns>
    public async Task<Result> HandleAsync(
        UpdateInventoryEntryCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await categories.GetByIdAsync(command.CategoryId, ct);
        if (category is null)
        {
            return Error.NotFound(ApplicationErrorCode.InventoryCategoryNotFound);
        }

        return category.UpdateEntry(command.EntryId, command.Quantity, command.Values);
    }
}
