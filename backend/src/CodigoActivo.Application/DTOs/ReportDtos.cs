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

public record EventRosterGuardianResponse(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone
);

public record EventRosterParticipantResponse(
    Guid UserId,
    string FirstName,
    string LastName,
    DateOnly BirthDate,
    string? Email,
    string? Phone,
    string RoleName,
    EventRosterGuardianResponse? Guardian
);

public record EventRosterActivityResponse(
    Guid ActivityId,
    string Title,
    string Location,
    DateTimeOffset ActivityStartsAt,
    DateTimeOffset ActivityEndsAt,
    IReadOnlyList<EventRosterParticipantResponse> Participants
);

public record EventRosterResponse(
    Guid EventId,
    string Title,
    IReadOnlyList<EventRosterActivityResponse> Activities
);

public record DashboardSummaryResponse(
    int Events,
    int Activities,
    int Resources,
    int Announcements,
    int Partners,
    int Users
);

public record DashboardKpiResponse(string Key, int Total, int InRange, int PreviousRange);

public record DashboardSeriesResponse(string Key, IReadOnlyList<int> Values);

public record DashboardTimeSeriesResponse(
    IReadOnlyList<DateOnly> Buckets,
    IReadOnlyList<DashboardSeriesResponse> Series
);

public record DashboardSliceResponse(string Key, string? Label, string? Color, int Count);

public record DashboardTopEventResponse(Guid EventId, string Title, int Confirmed);

public record DashboardOccupancyActivityResponse(
    Guid ActivityId,
    string Title,
    DateTimeOffset StartsAt,
    int Confirmed,
    int Desired
);

public record DashboardOccupancyEventResponse(
    Guid EventId,
    string Title,
    int Confirmed,
    int Desired,
    IReadOnlyList<DashboardOccupancyActivityResponse> Activities
);

public record DashboardOccupancyResponse(
    int Confirmed,
    int Desired,
    IReadOnlyList<DashboardOccupancyEventResponse> Events
);

public record DashboardAnalyticsResponse(
    DateOnly RangeStart,
    DateOnly RangeEnd,
    string Granularity,
    IReadOnlyList<DashboardKpiResponse> Kpis,
    DashboardTimeSeriesResponse UserGrowth,
    DashboardTimeSeriesResponse Inscriptions,
    DashboardTimeSeriesResponse ContentPublished,
    IReadOnlyList<DashboardSliceResponse> UsersByType,
    IReadOnlyList<DashboardSliceResponse> AudienceComposition,
    IReadOnlyList<DashboardSliceResponse> ResourcesByType,
    IReadOnlyList<DashboardSliceResponse> EventsByCategory,
    IReadOnlyList<DashboardTopEventResponse> TopEvents,
    DashboardTimeSeriesResponse EventsCalendar,
    DashboardOccupancyResponse Occupancy
);
