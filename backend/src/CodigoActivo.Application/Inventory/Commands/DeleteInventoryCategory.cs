using CodigoActivo.Domain.Inventory;

namespace CodigoActivo.Application.Inventory.Commands;

/// <summary>
/// Carries the input required to delete an inventory category.
/// </summary>
/// <param name="CategoryId">Identifier of the category.</param>
public sealed record DeleteInventoryCategoryCommand(InventoryCategoryId CategoryId)
    : ICommand<Result>,
        IRoutineCommand;

/// <summary>
/// Executes the command to delete an inventory category with its columns and entries.
/// </summary>
/// <param name="categories">Repository used to persist and retrieve inventory categories.</param>
public sealed class DeleteInventoryCategoryCommandHandler(IInventoryCategoryRepository categories)
    : ICommandHandler<DeleteInventoryCategoryCommand, Result>
{
    /// <summary>
    /// Handles the request to delete an inventory category.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result reports success, or
    /// <see cref="ApplicationErrorCode.InventoryCategoryNotFound"/>.
    /// </returns>
    public async Task<Result> HandleAsync(
        DeleteInventoryCategoryCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await categories.GetByIdAsync(command.CategoryId, ct);
        if (category is null)
        {
            return Error.NotFound(ApplicationErrorCode.InventoryCategoryNotFound);
        }

        categories.Remove(category);
        return Result.Success();
    }
}
