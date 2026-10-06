using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Users.Contracts;

namespace CodigoActivo.Application.Reports.Contracts;

/// <summary>
/// Carries the criteria used to event attendee list.
/// </summary>
public sealed class EventAttendeeListQuery : PageQuery
{
    /// <summary>
    /// Gets or sets the search value.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the associated user type.
    /// </summary>
    public Guid? UserTypeId { get; set; }

    /// <summary>
    /// Gets or sets the gender value.
    /// </summary>
    [EnumDataType(typeof(Gender))]
    public Gender? Gender { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the associated activity.
    /// </summary>
    public Guid? ActivityId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the associated role type.
    /// </summary>
    public Guid? RoleTypeId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the associated status.
    /// </summary>
    public Guid? StatusId { get; set; }
}
