namespace CodigoActivo.Application.DTOs;

public record EventSummaryResponse(
    Guid EventId,
    string Title,
    int ActivitiesCount,
    int TotalAssignments,
    int RequestedAssignments,
    int ConfirmedAssignments,
    int DeniedAssignments,
    int DistinctVolunteers,
    IReadOnlyList<EventRoleTypeSummaryResponse> RoleTypeBreakdown
);

public record EventRoleTypeSummaryResponse(
    Guid RoleTypeId,
    string? RoleTypeName,
    int ApprovedAssignments
);

public record EventAttendeeAssignmentResponse(
    Guid ActivityId,
    string ActivityTitle,
    DateTimeOffset ActivityStartsAt,
    DateTimeOffset ActivityEndsAt,
    Guid RoleTypeId,
    string? RoleTypeName,
    Guid StatusId,
    string? StatusName,
    DateTimeOffset SignedUpAt,
    bool HasTimeConflict
);

public record EventAttendeeGuardianResponse(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone
);

public record EventAttendeeResponse(
    Guid UserId,
    string? FirstName,
    string? LastName,
    string? Email,
    string? Phone,
    DateOnly BirthDate,
    string UserTypeName,
    string UserTypeColor,
    EventAttendeeGuardianResponse? Guardian,
    IReadOnlyList<EventAttendeeAssignmentResponse> Assignments
);

public record EventAttendeesResponse(
    Guid EventId,
    string Title,
    IReadOnlyList<EventAttendeeResponse> Attendees
);

public record EventBadgeGuardianResponse(string FirstName, string LastName, string? Phone);

public record EventBadgeResponse(
    Guid UserId,
    string FirstName,
    string LastName,
    string UserTypeName,
    string UserTypeColor,
    DateTimeOffset CreatedAt,
    EventBadgeGuardianResponse? Guardian,
    IReadOnlyList<string> Activities
);

public record EventBadgesResponse(
    Guid EventId,
    string Title,
    IReadOnlyList<EventBadgeResponse> Badges
);

public record DashboardSummaryResponse(
    int Events,
    int Activities,
    int Resources,
    int Announcements,
    int Partners,
    int Users
);
