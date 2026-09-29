using CodigoActivo.Application.Common.Querying;

namespace CodigoActivo.Application.Resources.Contracts;

/// <summary>
/// Carries the criteria used to resource list.
/// </summary>
public sealed class ResourceListQuery : PageQuery
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
    /// Gets or sets the identifier of the associated resource type.
    /// </summary>
    public Guid? ResourceTypeId { get; set; }

    /// <summary>
    /// Gets or sets the url value.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets the created from value.
    /// </summary>
    public DateOnly? CreatedFrom { get; set; }

    /// <summary>
    /// Gets or sets the created to value.
    /// </summary>
    public DateOnly? CreatedTo { get; set; }
}
