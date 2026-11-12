using CodigoActivo.Domain.Inventory;

namespace CodigoActivo.Application.Inventory.Commands;

/// <summary>
/// Carries the input required to create an inventory category.
/// </summary>
/// <param name="Name">
/// Name of up to <see cref="InventoryCategory.NameMaxLength"/> characters, unique among categories.
/// </param>
/// <param name="Columns">
/// From one to <see cref="InventoryCategory.MaxColumns"/> new columns in order.
/// </param>
public sealed record CreateInventoryCategoryCommand(
    [property: Required, MaxLength(InventoryCategory.NameMaxLength)] string Name,
    [property: Required, MinLength(1), MaxLength(InventoryCategory.MaxColumns)]
        IReadOnlyList<InventoryColumnDraft> Columns
) : ICommand<Result<InventoryCategoryId>>, IRoutineCommand;

/// <summary>
/// Executes the command to create an inventory category. Names are unique ignoring letter case.
/// </summary>
/// <param name="categories">Repository used to persist and retrieve inventory categories.</param>
public sealed class CreateInventoryCategoryCommandHandler(IInventoryCategoryRepository categories)
    : ICommandHandler<CreateInventoryCategoryCommand, Result<InventoryCategoryId>>
{
    /// <summary>
    /// Handles the request to create an inventory category.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the identifier of the new category;
    /// <see cref="ApplicationErrorCode.InventoryCategoryNameAlreadyExists"/>; or a failure of
    /// <see cref="InventoryCategory.Create"/> when the columns break its rules.
    /// </returns>
    public async Task<Result<InventoryCategoryId>> HandleAsync(
        CreateInventoryCategoryCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var name = command.Name.Trim();
        if (await categories.NameExistsAsync(name, ct: ct))
        {
            return Error.Conflict(ApplicationErrorCode.InventoryCategoryNameAlreadyExists);
        }

        var created = InventoryCategory.Create(
            name,
            [.. command.Columns.Select(column => column.ToDefinition())]
        );
        if (created.IsFailure)
        {
            return created.Error!;
        }

        await categories.AddAsync(created.Value, ct);
        return created.Value.Id;
    }
}
