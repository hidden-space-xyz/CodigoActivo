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

public record AssignmentReportItemResponse(
    Guid ActivityId,
    string ActivityTitle,
    Guid UserId,
    Guid RoleTypeId,
    string? RoleTypeName,
    Guid StatusId,
    string? StatusName
);

public record ActivityRoleTypeSummaryResponse(
    Guid RoleTypeId,
    string? RoleTypeName,
    int ApprovedAssignments
);

public record ActivityAssignmentRowResponse(
    Guid UserId,
    string? FirstName,
    string? LastName,
    string? Email,
    string? Phone,
    DateOnly BirthDate,
    Guid? ParentId,
    bool SignedUp,
    Guid? RoleTypeId,
    string? RoleTypeName,
    Guid? StatusId,
    string? StatusName
);

public record ActivityAssignmentsReportResponse(
    Guid ActivityId,
    string Title,
    int TotalSignups,
    IReadOnlyList<ActivityRoleTypeSummaryResponse> RoleTypeBreakdown,
    IReadOnlyList<ActivityAssignmentRowResponse> Rows
);

public record EventAssignmentsReportResponse(
    Guid EventId,
    string Title,
    IReadOnlyList<AssignmentReportItemResponse> Items
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