using CodigoActivo.Application.Common.Querying;

namespace CodigoActivo.Application.News.Contracts;

/// <summary>
/// Carries the criteria used to news list.
/// </summary>
public sealed class NewsListQuery : PageQuery
{
    /// <summary>
    /// Gets or sets the text matched against the title or the subtitle.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Gets or sets the title displayed to users.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the supporting subtitle displayed to users.
    /// </summary>
    public string? Subtitle { get; set; }

    /// <summary>
    /// Gets or sets whether the item is highlighted as featured.
    /// </summary>
    public bool? Featured { get; set; }

    /// <summary>
    /// Gets or sets the year value.
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// Gets or sets the created from value.
    /// </summary>
    public DateOnly? CreatedFrom { get; set; }

    /// <summary>
    /// Gets or sets the created to value.
    /// </summary>
    public DateOnly? CreatedTo { get; set; }
}
