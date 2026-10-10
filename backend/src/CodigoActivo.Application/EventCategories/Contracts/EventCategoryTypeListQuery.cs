using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Common.Validation;

namespace CodigoActivo.Application.EventCategories.Contracts;

/// <summary>
/// Carries the criteria used to event category type list.
/// </summary>
public sealed class EventCategoryTypeListQuery : PageQuery
{
    /// <summary>
    /// Gets or sets the human-readable name.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the display color associated with the item.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Color { get; set; }
}
