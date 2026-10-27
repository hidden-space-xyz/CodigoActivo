using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Common.Validation;

namespace CodigoActivo.Application.Resources.Contracts;

/// <summary>
/// Carries the criteria used to list resources; every criterion left empty matches every resource.
/// Text criteria match a part of the text, ignoring case and accents.
/// </summary>
public sealed class ResourceListQuery : PageQuery
{
    /// <summary>
    /// Gets or sets the text matched against the title or the subtitle.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Search { get; set; }

    /// <summary>
    /// Gets or sets the text the title must contain.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the text the subtitle must contain.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Subtitle { get; set; }

    /// <summary>
    /// Gets or sets the catalog identifier of the type the resource must have.
    /// </summary>
    public Guid? ResourceTypeId { get; set; }

    /// <summary>
    /// Gets or sets the text the link of an external resource must contain.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets the first local day of the range of creation dates.
    /// </summary>
    public DateOnly? CreatedFrom { get; set; }

    /// <summary>
    /// Gets or sets the last local day of the range of creation dates.
    /// </summary>
    public DateOnly? CreatedTo { get; set; }
}
