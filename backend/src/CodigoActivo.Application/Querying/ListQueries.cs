namespace CodigoActivo.Application.Querying;

public enum EventScope
{
    Upcoming,
    Past,
}

public sealed class EventListQuery : PageQuery
{
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public bool? Featured { get; set; }
    public EventScope? Scope { get; set; }
    public int? Year { get; set; }
    public Guid? CategoryTypeId { get; set; }
    public DateOnly? EventDateFrom { get; set; }
    public DateOnly? EventDateTo { get; set; }
    public DateOnly? SignupFrom { get; set; }
    public DateOnly? SignupTo { get; set; }
}

public sealed class ActivityListQuery : PageQuery
{
    public Guid? EventId { get; set; }
    public string? Title { get; set; }
    public Guid? ModalityTypeId { get; set; }
    public string? Location { get; set; }
    public DateOnly? ActivityDateFrom { get; set; }
    public DateOnly? ActivityDateTo { get; set; }
}

public sealed class EventCategoryTypeListQuery : PageQuery
{
    public string? Name { get; set; }
    public string? Color { get; set; }
}

public sealed class EventAttendeeListQuery : PageQuery
{
    public string? Search { get; set; }
    public Guid? UserTypeId { get; set; }
    public Guid? ActivityId { get; set; }
    public Guid? RoleTypeId { get; set; }
    public Guid? StatusId { get; set; }
}

public sealed class AnnouncementListQuery : PageQuery
{
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public bool? Featured { get; set; }
    public int? Year { get; set; }
    public DateOnly? CreatedFrom { get; set; }
    public DateOnly? CreatedTo { get; set; }
}

public sealed class ResourceListQuery : PageQuery
{
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public Guid? ResourceTypeId { get; set; }
    public string? Url { get; set; }
    public DateOnly? CreatedFrom { get; set; }
    public DateOnly? CreatedTo { get; set; }
}

public sealed class PartnerListQuery : PageQuery
{
    public string? Name { get; set; }
    public string? Website { get; set; }
    public int? Tier { get; set; }
    public DateOnly? FromDateFrom { get; set; }
    public DateOnly? FromDateTo { get; set; }
}

public sealed class UserListQuery : PageQuery
{
    public Guid? Id { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public Guid? UserTypeId { get; set; }
    public Guid? UserStatusTypeId { get; set; }
    public bool? IsAdmin { get; set; }
    public Guid? ParentId { get; set; }
    public DateOnly? BirthDateFrom { get; set; }
    public DateOnly? BirthDateTo { get; set; }
}
