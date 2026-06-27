namespace CodigoActivo.Application.DTOs;

public record EventSummaryResponse(
    Guid EventId,
    string Title,
    int ActivitiesCount,
    int TotalAssignments,
    int RequestedAssignments,
    int ConfirmedAssignments,
    int DeniedAssignments,
    int DistinctVolunteers
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

public record EventAssignmentsReportResponse(
    Guid EventId,
    string Title,
    IReadOnlyList<AssignmentReportItemResponse> Items
);

public record DashboardSummaryResponse(
    int Events,
    int Activities,
    int Resources,
    int Announcements,
    int Partners,
    int Users
);
