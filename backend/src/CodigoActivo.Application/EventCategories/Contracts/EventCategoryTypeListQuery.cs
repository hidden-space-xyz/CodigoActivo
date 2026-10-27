using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Common.Validation;

namespace CodigoActivo.Application.EventCategories.Contracts;

/// <summary>
/// Carries the criteria used to list event category types; every criterion left empty matches
/// every category type. Text criteria match a part of the text, ignoring case and accents.
/// </summary>
public sealed class EventCategoryTypeListQuery : PageQuery
{
    /// <summary>
    /// Gets or sets the text the name must contain.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the text the <c>#RRGGBB</c> color must contain.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Color { get; set; }
}
