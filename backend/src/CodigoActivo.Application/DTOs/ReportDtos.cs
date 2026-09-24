using CodigoActivo.Domain.Entities;

namespace CodigoActivo.Application.DTOs;

/// <summary>
/// Contains the event data returned by the API.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="Title">The title value.</param>
/// <param name="ActivitiesCount">Number of activities allowed or reported.</param>
/// <param name="TotalAssignments">The total assignments value.</param>
/// <param name="RequestedAssignments">The requested assignments value.</param>
/// <param name="ConfirmedAssignments">The confirmed assignments value.</param>
/// <param name="DeniedAssignments">The denied assignments value.</param>
/// <param name="DistinctVolunteers">The distinct volunteers value.</param>
/// <param name="RatingsCount">Number of ratings allowed or reported.</param>
/// <param name="RatingsAverage">The ratings average value.</param>
/// <param name="RoleTypeBreakdown">The role type breakdown value.</param>
public record EventSummaryResponse(
    Guid EventId,
    string Title,
    int ActivitiesCount,
    int TotalAssignments,
    int RequestedAssignments,
    int ConfirmedAssignments,
    int DeniedAssignments,
    int DistinctVolunteers,
    int RatingsCount,
    double? RatingsAverage,
    IReadOnlyList<EventRoleTypeSummaryResponse> RoleTypeBreakdown
);

/// <summary>
/// Contains the event role type data returned by the API.
/// </summary>
/// <param name="RoleTypeId">Identifier of the role type.</param>
/// <param name="RoleTypeName">The role type name value.</param>
/// <param name="ApprovedAssignments">The approved assignments value.</param>
public record EventRoleTypeSummaryResponse(
    Guid RoleTypeId,
    string? RoleTypeName,
    int ApprovedAssignments
);

/// <summary>
/// Contains the event attendee assignment data returned by the API.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="ActivityTitle">The activity title value.</param>
/// <param name="ActivityStartsAt">The activity starts at value.</param>
/// <param name="ActivityEndsAt">The activity ends at value.</param>
/// <param name="RoleTypeId">Identifier of the role type.</param>
/// <param name="RoleTypeName">The role type name value.</param>
/// <param name="StatusId">Identifier of the status.</param>
/// <param name="StatusName">The status name value.</param>
/// <param name="SignedUpAt">The signed up at value.</param>
/// <param name="HasTimeConflict">Whether has time conflict.</param>
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

/// <summary>
/// Contains the event attendee guardian data returned by the API.
/// </summary>
/// <param name="FirstName">User's given name.</param>
/// <param name="LastName">User's family name.</param>
/// <param name="Email">Email address to validate or locate.</param>
/// <param name="Phone">Phone number to validate or locate.</param>
public record EventAttendeeGuardianResponse(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone
);

/// <summary>
/// Contains the event attendee data returned by the API.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="FirstName">User's given name.</param>
/// <param name="LastName">User's family name.</param>
/// <param name="Email">Email address to validate or locate.</param>
/// <param name="Phone">Phone number to validate or locate.</param>
/// <param name="BirthDate">User's date of birth; only dependents have one.</param>
/// <param name="Gender">The gender value.</param>
/// <param name="UserTypeName">The user type name value.</param>
/// <param name="UserTypeColor">The user type color value.</param>
/// <param name="Guardian">The guardian value.</param>
/// <param name="Assignments">The assignments value.</param>
public record EventAttendeeResponse(
    Guid UserId,
    string? FirstName,
    string? LastName,
    string? Email,
    string? Phone,
    DateOnly? BirthDate,
    Gender Gender,
    string UserTypeName,
    string UserTypeColor,
    EventAttendeeGuardianResponse? Guardian,
    IReadOnlyList<EventAttendeeAssignmentResponse> Assignments
);

/// <summary>
/// Contains the event badge guardian data returned by the API.
/// </summary>
/// <param name="FirstName">User's given name.</param>
/// <param name="LastName">User's family name.</param>
/// <param name="Phone">Phone number to validate or locate.</param>
public record EventBadgeGuardianResponse(string FirstName, string LastName, string? Phone);

/// <summary>
/// Contains the event badge data returned by the API.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="FirstName">User's given name.</param>
/// <param name="LastName">User's family name.</param>
/// <param name="UserTypeName">The user type name value.</param>
/// <param name="UserTypeColor">The user type color value.</param>
/// <param name="CreatedAt">UTC timestamp when the record was created.</param>
/// <param name="Guardian">The guardian value.</param>
/// <param name="Activities">The activities value.</param>
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

/// <summary>
/// Contains the event badges data returned by the API.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="Title">The title value.</param>
/// <param name="Badges">The badges value.</param>
public record EventBadgesResponse(
    Guid EventId,
    string Title,
    IReadOnlyList<EventBadgeResponse> Badges
);

/// <summary>
/// Contains the event roster guardian data returned by the API.
/// </summary>
/// <param name="FirstName">User's given name.</param>
/// <param name="LastName">User's family name.</param>
/// <param name="Email">Email address to validate or locate.</param>
/// <param name="Phone">Phone number to validate or locate.</param>
public record EventRosterGuardianResponse(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone
);

/// <summary>
/// Contains the event roster participant data returned by the API.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="FirstName">User's given name.</param>
/// <param name="LastName">User's family name.</param>
/// <param name="BirthDate">User's date of birth; only dependents have one.</param>
/// <param name="Email">Email address to validate or locate.</param>
/// <param name="Phone">Phone number to validate or locate.</param>
/// <param name="RoleName">The role name value.</param>
/// <param name="Guardian">The guardian value.</param>
public record EventRosterParticipantResponse(
    Guid UserId,
    string FirstName,
    string LastName,
    DateOnly? BirthDate,
    string? Email,
    string? Phone,
    string RoleName,
    EventRosterGuardianResponse? Guardian
);

/// <summary>
/// Contains the event roster activity data returned by the API.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="Title">The title value.</param>
/// <param name="Location">The location value.</param>
/// <param name="ActivityStartsAt">The activity starts at value.</param>
/// <param name="ActivityEndsAt">The activity ends at value.</param>
/// <param name="Participants">The participants value.</param>
public record EventRosterActivityResponse(
    Guid ActivityId,
    string Title,
    string Location,
    DateTimeOffset ActivityStartsAt,
    DateTimeOffset ActivityEndsAt,
    IReadOnlyList<EventRosterParticipantResponse> Participants
);

/// <summary>
/// Contains the event roster data returned by the API.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="Title">The title value.</param>
/// <param name="Activities">The activities value.</param>
public record EventRosterResponse(
    Guid EventId,
    string Title,
    IReadOnlyList<EventRosterActivityResponse> Activities
);

/// <summary>
/// Contains the dashboard data returned by the API.
/// </summary>
/// <param name="Events">The events value.</param>
/// <param name="Activities">The activities value.</param>
/// <param name="Resources">The resources value.</param>
/// <param name="Announcements">The announcements value.</param>
/// <param name="Partners">The partners value.</param>
/// <param name="Users">The users value.</param>
public record DashboardSummaryResponse(
    int Events,
    int Activities,
    int Resources,
    int Announcements,
    int Partners,
    int Users
);

/// <summary>
/// Contains the dashboard kpi data returned by the API.
/// </summary>
/// <param name="Key">The key value.</param>
/// <param name="Total">Number of total allowed or reported.</param>
/// <param name="InRange">The in range value.</param>
/// <param name="PreviousRange">The previous range value.</param>
public record DashboardKpiResponse(string Key, int Total, int InRange, int PreviousRange);

/// <summary>
/// Contains the dashboard series data returned by the API.
/// </summary>
/// <param name="Key">The key value.</param>
/// <param name="Values">The values value.</param>
public record DashboardSeriesResponse(string Key, IReadOnlyList<int> Values);

/// <summary>
/// Contains the dashboard time series data returned by the API.
/// </summary>
/// <param name="Buckets">The buckets value.</param>
/// <param name="Series">The series value.</param>
public record DashboardTimeSeriesResponse(
    IReadOnlyList<DateOnly> Buckets,
    IReadOnlyList<DashboardSeriesResponse> Series
);

/// <summary>
/// Contains the dashboard slice data returned by the API.
/// </summary>
/// <param name="Key">The key value.</param>
/// <param name="Label">The label value.</param>
/// <param name="Color">The color value.</param>
/// <param name="Count">Number of records represented by this item.</param>
public record DashboardSliceResponse(string Key, string? Label, string? Color, int Count);

/// <summary>
/// Contains the dashboard top event data returned by the API.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="Title">The title value.</param>
/// <param name="Confirmed">The confirmed value.</param>
public record DashboardTopEventResponse(Guid EventId, string Title, int Confirmed);

/// <summary>
/// Contains the dashboard occupancy activity data returned by the API.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="Title">The title value.</param>
/// <param name="StartsAt">The starts at value.</param>
/// <param name="Confirmed">The confirmed value.</param>
/// <param name="Desired">The desired value.</param>
public record DashboardOccupancyActivityResponse(
    Guid ActivityId,
    string Title,
    DateTimeOffset StartsAt,
    int Confirmed,
    int Desired
);

/// <summary>
/// Contains the dashboard occupancy event data returned by the API.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="Title">The title value.</param>
/// <param name="Confirmed">The confirmed value.</param>
/// <param name="Desired">The desired value.</param>
/// <param name="Activities">The activities value.</param>
public record DashboardOccupancyEventResponse(
    Guid EventId,
    string Title,
    int Confirmed,
    int Desired,
    IReadOnlyList<DashboardOccupancyActivityResponse> Activities
);

/// <summary>
/// Contains the dashboard occupancy data returned by the API.
/// </summary>
/// <param name="Confirmed">The confirmed value.</param>
/// <param name="Desired">The desired value.</param>
/// <param name="Events">The events value.</param>
public record DashboardOccupancyResponse(
    int Confirmed,
    int Desired,
    IReadOnlyList<DashboardOccupancyEventResponse> Events
);

/// <summary>
/// Contains the dashboard analytics data returned by the API.
/// </summary>
/// <param name="RangeStart">The range start value.</param>
/// <param name="RangeEnd">The range end value.</param>
/// <param name="Granularity">The granularity value.</param>
/// <param name="Kpis">The kpis value.</param>
/// <param name="UserGrowth">The user growth value.</param>
/// <param name="Inscriptions">The inscriptions value.</param>
/// <param name="ContentPublished">The content published value.</param>
/// <param name="UsersByType">The users by type value.</param>
/// <param name="AudienceComposition">The audience composition value.</param>
/// <param name="ParticipantsByGender">The participants by gender value.</param>
/// <param name="EventsByCategory">The events by category value.</param>
/// <param name="TopEvents">The top events value.</param>
/// <param name="EventsCalendar">The events calendar value.</param>
/// <param name="Occupancy">The occupancy value.</param>
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
    IReadOnlyList<DashboardSliceResponse> ParticipantsByGender,
    IReadOnlyList<DashboardSliceResponse> EventsByCategory,
    IReadOnlyList<DashboardTopEventResponse> TopEvents,
    DashboardTimeSeriesResponse EventsCalendar,
    DashboardOccupancyResponse Occupancy
);
