using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Common.Validation;

namespace CodigoActivo.Application.Reports.Contracts;

/// <summary>
/// Carries the criteria used to list the attendees of an event; every criterion left empty matches
/// every attendee.
/// </summary>
public sealed class EventAttendeeListQuery : PageQuery
{
    /// <summary>
    /// Gets or sets the text the names, email or phones of the attendee or their guardian must
    /// contain, ignoring case and accents.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Search { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the membership type the attendee must have.
    /// </summary>
    public Guid? UserTypeId { get; set; }

    /// <summary>
    /// Gets or sets the gender the attendee must have.
    /// </summary>
    [EnumDataType(typeof(Gender))]
    public Gender? Gender { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the activity the attendee must be signed up to.
    /// </summary>
    public Guid? ActivityId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the role the attendee must be signed up in.
    /// </summary>
    public Guid? RoleTypeId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the status a signup of the attendee must have.
    /// </summary>
    public Guid? StatusId { get; set; }
}
