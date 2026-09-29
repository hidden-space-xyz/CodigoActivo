using CodigoActivo.Application.Common.Querying;

namespace CodigoActivo.Application.EventCategories.Contracts;

/// <summary>
/// Carries the criteria used to event category type list.
/// </summary>
public sealed class EventCategoryTypeListQuery : PageQuery
{
    /// <summary>
    /// Gets or sets the human-readable name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the display color associated with the item.
    /// </summary>
    public string? Color { get; set; }
}
