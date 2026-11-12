using CodigoActivo.Domain.Inventory;

namespace CodigoActivo.Application.Inventory.Commands;

/// <summary>
/// Carries the input required to delete an inventory entry.
/// </summary>
/// <param name="CategoryId">Identifier of the category.</param>
/// <param name="EntryId">Identifier of the entry.</param>
public sealed record DeleteInventoryEntryCommand(
    InventoryCategoryId CategoryId,
    InventoryEntryId EntryId
) : ICommand<Result>, IRoutineCommand;

/// <summary>
/// Executes the command to delete an inventory entry.
/// </summary>
/// <param name="categories">Repository used to persist and retrieve inventory categories.</param>
public sealed class DeleteInventoryEntryCommandHandler(IInventoryCategoryRepository categories)
    : ICommandHandler<DeleteInventoryEntryCommand, Result>
{
    /// <summary>
    /// Handles the request to delete an inventory entry.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result reports success;
    /// <see cref="ApplicationErrorCode.InventoryCategoryNotFound"/>; or
    /// <see cref="DomainErrorCode.InventoryEntryNotFound"/>.
    /// </returns>
    public async Task<Result> HandleAsync(
        DeleteInventoryEntryCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await categories.GetByIdAsync(command.CategoryId, ct);
        if (category is null)
        {
            return Error.NotFound(ApplicationErrorCode.InventoryCategoryNotFound);
        }

        return category.RemoveEntry(command.EntryId);
    }
}
