using CodigoActivo.Domain.Inventory;

namespace CodigoActivo.Application.Inventory.Commands;

/// <summary>
/// Carries the input required to rename an inventory category and replace its columns.
/// </summary>
/// <param name="CategoryId">Identifier of the category.</param>
/// <param name="Name">
/// Name of up to <see cref="InventoryCategory.NameMaxLength"/> characters, unique among categories.
/// </param>
/// <param name="Columns">
/// From one to <see cref="InventoryCategory.MaxColumns"/> columns in order: existing ones by their
/// identifier and new ones without; a column left out is removed with its values.
/// </param>
public sealed record UpdateInventoryCategoryCommand(
    InventoryCategoryId CategoryId,
    [property: Required, MaxLength(InventoryCategory.NameMaxLength)] string Name,
    [property: Required, MinLength(1), MaxLength(InventoryCategory.MaxColumns)]
        IReadOnlyList<InventoryColumnDraft> Columns
) : ICommand<Result>, IRoutineCommand;

/// <summary>
/// Executes the command to rename an inventory category and replace its columns. Names are unique
/// ignoring letter case.
/// </summary>
/// <param name="categories">Repository used to persist and retrieve inventory categories.</param>
public sealed class UpdateInventoryCategoryCommandHandler(IInventoryCategoryRepository categories)
    : ICommandHandler<UpdateInventoryCategoryCommand, Result>
{
    /// <summary>
    /// Handles the request to update an inventory category.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result reports success;
    /// <see cref="ApplicationErrorCode.InventoryCategoryNotFound"/>;
    /// <see cref="ApplicationErrorCode.InventoryCategoryNameAlreadyExists"/>; or a failure of
    /// <see cref="InventoryCategory.Redefine"/>, in which case nothing changes.
    /// </returns>
    public async Task<Result> HandleAsync(
        UpdateInventoryCategoryCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await categories.GetByIdAsync(command.CategoryId, ct);
        if (category is null)
        {
            return Error.NotFound(ApplicationErrorCode.InventoryCategoryNotFound);
        }

        var name = command.Name.Trim();
        if (await categories.NameExistsAsync(name, command.CategoryId, ct))
        {
            return Error.Conflict(ApplicationErrorCode.InventoryCategoryNameAlreadyExists);
        }

        return category.Redefine(
            name,
            [.. command.Columns.Select(column => column.ToDefinition())]
        );
    }
}
