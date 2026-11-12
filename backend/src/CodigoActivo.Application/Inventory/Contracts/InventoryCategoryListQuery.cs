namespace CodigoActivo.Application.Inventory.Contracts;

/// <summary>
/// Carries the criteria used to list inventory categories; every criterion left empty matches every
/// category. Text criteria match a part of the text, ignoring case and accents.
/// </summary>
public sealed class InventoryCategoryListQuery : PageQuery
{
    /// <summary>
    /// Gets or sets the text the name must contain.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Name { get; set; }
}
