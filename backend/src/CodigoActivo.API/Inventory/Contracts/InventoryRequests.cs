using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Common.Mapping;
using CodigoActivo.Application.Inventory.Commands;
using CodigoActivo.Domain.Inventory;
using InventoryColumnType = CodigoActivo.Application.Abstractions.Querying.ReadModel.InventoryColumnType;

namespace CodigoActivo.API.Inventory.Contracts;

/// <summary>
/// Contains the client-supplied data of a column of an inventory category.
/// </summary>
/// <param name="Id">Identifier of an existing column of the category; <see langword="null"/> adds one.</param>
/// <param name="Name">Name, unique within the category ignoring letter case.</param>
/// <param name="Type">
/// Kind of value the column holds; a category has at most one quantity column, and the type of a
/// column cannot change while an entry has a value in it.
/// </param>
/// <param name="Required">
/// Whether every entry must have a value in it; refused while an entry has none.
/// </param>
/// <param name="Options">
/// Options of a list column in order, at least one; empty for any other type.
/// </param>
public record InventoryColumnRequest(
    Guid? Id,
    [Required] [MaxLength(InventoryColumn.NameMaxLength)] string Name,
    [EnumDataType(typeof(InventoryColumnType))] InventoryColumnType Type,
    bool Required,
    [Required]
    [MaxLength(InventoryColumn.MaxOptions)]
        IReadOnlyList<InventoryColumnOptionRequest> Options
)
{
    /// <summary>
    /// Builds the draft of the column.
    /// </summary>
    /// <returns>The draft.</returns>
    public InventoryColumnDraft ToDraft()
    {
        return new InventoryColumnDraft(
            Id is { } id ? InventoryColumnId.From(id) : null,
            Name,
            Type.ToDomain(),
            Required,
            [.. Options.Select(option => option.ToDraft())]
        );
    }
}

/// <summary>
/// Contains the client-supplied data of an option of a list column.
/// </summary>
/// <param name="Id">Identifier of an existing option of the column; <see langword="null"/> adds one.</param>
/// <param name="Name">Name, unique within the column ignoring letter case.</param>
/// <param name="Color">Display color as <c>#RRGGBB</c>.</param>
public record InventoryColumnOptionRequest(
    Guid? Id,
    [Required] [MaxLength(InventoryColumnOption.NameMaxLength)] string Name,
    [Required] [MaxLength(9)] [RegularExpression("^#[0-9A-Fa-f]{6}$")] string Color
)
{
    /// <summary>
    /// Builds the draft of the option.
    /// </summary>
    /// <returns>The draft.</returns>
    public InventoryColumnOptionDraft ToDraft()
    {
        return new InventoryColumnOptionDraft(
            Id is { } id ? InventoryColumnOptionId.From(id) : null,
            Name,
            Color
        );
    }
}

/// <summary>
/// Contains the client-supplied data used to create an inventory category.
/// </summary>
/// <param name="Name">Name, unique among categories ignoring letter case.</param>
/// <param name="Columns">New columns in order, without identifiers.</param>
public record CreateInventoryCategoryRequest(
    [Required] [MaxLength(InventoryCategory.NameMaxLength)] string Name,
    [Required]
    [MinLength(1)]
    [MaxLength(InventoryCategory.MaxColumns)]
        IReadOnlyList<InventoryColumnRequest> Columns
)
{
    /// <summary>
    /// Builds the command that creates the category.
    /// </summary>
    /// <returns>The command.</returns>
    public CreateInventoryCategoryCommand ToCommand()
    {
        return new CreateInventoryCategoryCommand(
            Name,
            [.. Columns.Select(column => column.ToDraft())]
        );
    }
}

/// <summary>
/// Contains the client-supplied data used to rename an inventory category and replace its columns.
/// </summary>
/// <param name="Name">Name, unique among categories ignoring letter case.</param>
/// <param name="Columns">
/// Columns in order: existing ones by their identifier and new ones without; a column left out is
/// removed with its values.
/// </param>
public record UpdateInventoryCategoryRequest(
    [Required] [MaxLength(InventoryCategory.NameMaxLength)] string Name,
    [Required]
    [MinLength(1)]
    [MaxLength(InventoryCategory.MaxColumns)]
        IReadOnlyList<InventoryColumnRequest> Columns
)
{
    /// <summary>
    /// Builds the command that updates the category.
    /// </summary>
    /// <param name="categoryId">Identifier of the category.</param>
    /// <returns>The command.</returns>
    public UpdateInventoryCategoryCommand ToCommand(InventoryCategoryId categoryId)
    {
        return new UpdateInventoryCategoryCommand(
            categoryId,
            Name,
            [.. Columns.Select(column => column.ToDraft())]
        );
    }
}

/// <summary>
/// Contains the client-supplied data used to add an entry to an inventory category.
/// </summary>
/// <param name="Quantity">
/// Value of the quantity column, or <see langword="null"/> for none or when the category has no
/// such column.
/// </param>
/// <param name="Values">
/// Value of each other column, keyed by column identifier: text as written, a number with a dot, a
/// day as <c>YYYY-MM-DD</c>, <c>true</c> or <c>false</c>, or the identifier of the chosen option.
/// A blank or missing value leaves the column empty; a required column cannot be left empty.
/// </param>
public record AddInventoryEntryRequest(
    [Range(0, InventoryEntry.MaxQuantity)] int? Quantity,
    [Required] [MaxLength(InventoryCategory.MaxColumns)] IReadOnlyDictionary<Guid, string> Values
)
{
    /// <summary>
    /// Builds the command that adds the entry.
    /// </summary>
    /// <param name="categoryId">Identifier of the category.</param>
    /// <returns>The command.</returns>
    public AddInventoryEntryCommand ToCommand(InventoryCategoryId categoryId)
    {
        return new AddInventoryEntryCommand(
            categoryId,
            Quantity,
            InventoryRequestMapping.Values(Values)
        );
    }
}

/// <summary>
/// Contains the client-supplied data that replaces the values of an inventory entry.
/// </summary>
/// <param name="Quantity">
/// Value of the quantity column, or <see langword="null"/> for none or when the category has no
/// such column.
/// </param>
/// <param name="Values">
/// Value of each other column, keyed by column identifier, written as when the entry was added.
/// A blank or missing value leaves the column empty; a required column cannot be left empty.
/// </param>
public record UpdateInventoryEntryRequest(
    [Range(0, InventoryEntry.MaxQuantity)] int? Quantity,
    [Required] [MaxLength(InventoryCategory.MaxColumns)] IReadOnlyDictionary<Guid, string> Values
)
{
    /// <summary>
    /// Builds the command that replaces the values of the entry.
    /// </summary>
    /// <param name="categoryId">Identifier of the category.</param>
    /// <param name="entryId">Identifier of the entry.</param>
    /// <returns>The command.</returns>
    public UpdateInventoryEntryCommand ToCommand(
        InventoryCategoryId categoryId,
        InventoryEntryId entryId
    )
    {
        return new UpdateInventoryEntryCommand(
            categoryId,
            entryId,
            Quantity,
            InventoryRequestMapping.Values(Values)
        );
    }
}

/// <summary>
/// Turns the values of an entry request, keyed by raw column identifier, into those of a command.
/// </summary>
internal static class InventoryRequestMapping
{
    /// <summary>
    /// Keys each value by its typed column identifier.
    /// </summary>
    /// <param name="values">Values keyed by raw column identifier.</param>
    /// <returns>The same values keyed by typed column identifier.</returns>
    public static IReadOnlyDictionary<InventoryColumnId, string> Values(
        IReadOnlyDictionary<Guid, string> values
    )
    {
        return values.ToDictionary(pair => InventoryColumnId.From(pair.Key), pair => pair.Value);
    }
}
