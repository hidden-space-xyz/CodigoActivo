using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Common.Validation;

namespace CodigoActivo.Application.Activities.Contracts;

/// <summary>
/// Carries the criteria used to list activities; every criterion left empty matches every activity.
/// </summary>
public sealed class ActivityListQuery : PageQuery
{
    /// <summary>
    /// Gets or sets the identifier of the event whose activities are listed.
    /// </summary>
    public Guid? EventId { get; set; }

    /// <summary>
    /// Gets or sets the text the title must contain, ignoring case and accents.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the modality the activities must have.
    /// </summary>
    public Guid? ModalityTypeId { get; set; }

    /// <summary>
    /// Gets or sets the text the location must contain, ignoring case and accents.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Location { get; set; }

    /// <summary>
    /// Gets or sets the first local day of the range; activities that end on or after it match.
    /// </summary>
    public DateOnly? ActivityDateFrom { get; set; }

    /// <summary>
    /// Gets or sets the last local day of the range; activities that start on or before it match.
    /// </summary>
    public DateOnly? ActivityDateTo { get; set; }
}
