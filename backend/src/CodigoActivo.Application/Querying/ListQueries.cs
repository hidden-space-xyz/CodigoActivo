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
}

public sealed class ActivityListQuery : PageQuery
{
    public Guid? EventId { get; set; }
    public string? Title { get; set; }
    public Guid? ModalityTypeId { get; set; }
}

public sealed class EventCategoryTypeListQuery : PageQuery
{
    public string? Name { get; set; }
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
}

public sealed class ResourceListQuery : PageQuery
{
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
}

public sealed class PartnerListQuery : PageQuery
{
    public string? Name { get; set; }
    public string? Website { get; set; }
    public int? Tier { get; set; }
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
}
