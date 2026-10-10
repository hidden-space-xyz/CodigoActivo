using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Common.Validation;

namespace CodigoActivo.Application.Resources.Contracts;

/// <summary>
/// Carries the criteria used to resource list.
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
    /// Gets or sets the title displayed to users.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the supporting subtitle displayed to users.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Subtitle { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the associated resource type.
    /// </summary>
    public Guid? ResourceTypeId { get; set; }

    /// <summary>
    /// Gets or sets the url value.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
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
