using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Common.Validation;

namespace CodigoActivo.Application.News.Contracts;

/// <summary>
/// Carries the criteria used to list news items; every criterion left empty matches every item.
/// Text criteria match a part of the text, ignoring case and accents.
/// </summary>
public sealed class NewsListQuery : PageQuery
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
    /// Gets or sets whether only the featured item, or only the others, are listed.
    /// </summary>
    public bool? Featured { get; set; }

    /// <summary>
    /// Gets or sets the year, in UTC, the item must have been created in; a year outside 1 to
    /// 9999 matches nothing.
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// Gets or sets the first local day of the range of creation dates.
    /// </summary>
    public DateOnly? CreatedFrom { get; set; }

    /// <summary>
    /// Gets or sets the last local day of the range of creation dates.
    /// </summary>
    public DateOnly? CreatedTo { get; set; }
}
