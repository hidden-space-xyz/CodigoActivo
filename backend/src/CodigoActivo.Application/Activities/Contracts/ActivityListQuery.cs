using CodigoActivo.Application.Common.Querying;

namespace CodigoActivo.Application.Activities.Contracts;

/// <summary>
/// Carries the criteria used to activity list.
/// </summary>
public sealed class ActivityListQuery : PageQuery
{
    /// <summary>
    /// Gets or sets the identifier of the associated event.
    /// </summary>
    public Guid? EventId { get; set; }

    /// <summary>
    /// Gets or sets the title displayed to users.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the associated modality type.
    /// </summary>
    public Guid? ModalityTypeId { get; set; }

    /// <summary>
    /// Gets or sets the location value.
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Gets or sets the activity date from value.
    /// </summary>
    public DateOnly? ActivityDateFrom { get; set; }

    /// <summary>
    /// Gets or sets the activity date to value.
    /// </summary>
    public DateOnly? ActivityDateTo { get; set; }
}
