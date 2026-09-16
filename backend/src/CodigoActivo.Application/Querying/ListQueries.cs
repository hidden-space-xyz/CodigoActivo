using CodigoActivo.Domain.Entities;

namespace CodigoActivo.Application.Querying;

/// <summary>
/// Identifies the supported event scope values.
/// </summary>
public enum EventScope
{
    /// <summary>
    /// Selects the upcoming option.
    /// </summary>
    Upcoming,
    /// <summary>
    /// Selects the past option.
    /// </summary>
    Past,
}

/// <summary>
/// Carries the criteria used to event list.
/// </summary>
public sealed class EventListQuery : PageQuery
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
    /// Gets or sets the scope value.
    /// </summary>
    public EventScope? Scope { get; set; }
    /// <summary>
    /// Gets or sets the year value.
    /// </summary>
    public int? Year { get; set; }
    /// <summary>
    /// Gets or sets the identifier of the associated category type.
    /// </summary>
    public Guid? CategoryTypeId { get; set; }
    /// <summary>
    /// Gets or sets the event date from value.
    /// </summary>
    public DateOnly? EventDateFrom { get; set; }
    /// <summary>
    /// Gets or sets the event date to value.
    /// </summary>
    public DateOnly? EventDateTo { get; set; }
    /// <summary>
    /// Gets or sets the signup from value.
    /// </summary>
    public DateOnly? SignupFrom { get; set; }
    /// <summary>
    /// Gets or sets the signup to value.
    /// </summary>
    public DateOnly? SignupTo { get; set; }
}

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

/// <summary>
/// Carries the criteria used to event category type list.
/// </summary>
public sealed class EventCategoryTypeListQuery : PageQuery
{
    /// <summary>
    /// Gets or sets the human-readable name.
    /// </summary>
    public string? Name { get; set; }
    /// <summary>
    /// Gets or sets the display color associated with the item.
    /// </summary>
    public string? Color { get; set; }
}

/// <summary>
/// Carries the criteria used to terms document list.
/// </summary>
public sealed class TermsDocumentListQuery : PageQuery
{
    /// <summary>
    /// Gets or sets the human-readable name.
    /// </summary>
    public string? Name { get; set; }
}

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

/// <summary>
/// Carries the criteria used to event rating list.
/// </summary>
public sealed class EventRatingListQuery : PageQuery;

/// <summary>
/// Carries the criteria used to announcement list.
/// </summary>
public sealed class AnnouncementListQuery : PageQuery
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

/// <summary>
/// Carries the criteria used to partner list.
/// </summary>
public sealed class PartnerListQuery : PageQuery
{
    /// <summary>
    /// Gets or sets the human-readable name.
    /// </summary>
    public string? Name { get; set; }
    /// <summary>
    /// Gets or sets the website value.
    /// </summary>
    public string? Website { get; set; }
    /// <summary>
    /// Gets or sets the tier value.
    /// </summary>
    public int? Tier { get; set; }
    /// <summary>
    /// Gets or sets the from date from value.
    /// </summary>
    public DateOnly? FromDateFrom { get; set; }
    /// <summary>
    /// Gets or sets the from date to value.
    /// </summary>
    public DateOnly? FromDateTo { get; set; }
}

/// <summary>
/// Carries the criteria used to user list.
/// </summary>
public sealed class UserListQuery : PageQuery
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public Guid? Id { get; set; }
    /// <summary>
    /// Gets or sets the human-readable name.
    /// </summary>
    public string? Name { get; set; }
    /// <summary>
    /// Gets or sets the email value.
    /// </summary>
    public string? Email { get; set; }
    /// <summary>
    /// Gets or sets the phone value.
    /// </summary>
    public string? Phone { get; set; }
    /// <summary>
    /// Gets or sets the identifier of the associated user type.
    /// </summary>
    public Guid? UserTypeId { get; set; }
    /// <summary>
    /// Gets or sets the identifier of the associated user status type.
    /// </summary>
    public Guid? UserStatusTypeId { get; set; }
    /// <summary>
    /// Gets or sets the is admin value.
    /// </summary>
    public bool? IsAdmin { get; set; }
    /// <summary>
    /// Gets or sets the identifier of the associated parent.
    /// </summary>
    public Guid? ParentId { get; set; }
    /// <summary>
    /// Gets or sets the birth date from value.
    /// </summary>
    public DateOnly? BirthDateFrom { get; set; }
    /// <summary>
    /// Gets or sets the birth date to value.
    /// </summary>
    public DateOnly? BirthDateTo { get; set; }
}
