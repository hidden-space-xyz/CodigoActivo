using CodigoActivo.Application.Abstractions.Querying.ReadModel;

namespace CodigoActivo.Application.Reports.Contracts;

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
/// <param name="RatingsCount">Number of ratings received, with or without a score.</param>
/// <param name="ScoredRatingsCount">Number of ratings that carry a score, the ones the average covers.</param>
/// <param name="RatingsAverage">Mean score of the scored ratings, or <see langword="null"/> without any.</param>
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
    int ScoredRatingsCount,
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
/// Full contact details of the guardian of a dependent, the administrator's view in the attendee list
/// and the event roster; the leader roster and the badges show less on purpose and keep their own
/// shapes.
/// </summary>
/// <param name="FirstName">Guardian's given name.</param>
/// <param name="LastName">Guardian's family name.</param>
/// <param name="Email">Guardian's email address.</param>
/// <param name="Phone">Guardian's primary phone.</param>
/// <param name="SecondaryPhone">Optional second contact phone of the guardian.</param>
public record EventGuardianContactResponse(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? SecondaryPhone
);

/// <summary>
/// Contains the event attendee data returned by the API.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="FirstName">User's given name.</param>
/// <param name="LastName">User's family name.</param>
/// <param name="Email">Email address to validate or locate.</param>
/// <param name="Phone">Phone number to validate or locate.</param>
/// <param name="SecondaryPhone">Optional second contact phone of the attendee.</param>
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
    string? SecondaryPhone,
    DateOnly? BirthDate,
    Gender Gender,
    string UserTypeName,
    string UserTypeColor,
    EventGuardianContactResponse? Guardian,
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
/// Contains one confirmed activity printed on an event badge, without its modality.
/// </summary>
/// <param name="Title">The title value.</param>
/// <param name="Location">Concrete place or platform where the activity takes place.</param>
public record EventBadgeActivityResponse(string Title, string Location);

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
/// <param name="Activities">Confirmed activities in chronological order.</param>
public record EventBadgeResponse(
    Guid UserId,
    string FirstName,
    string LastName,
    string UserTypeName,
    string UserTypeColor,
    DateTimeOffset CreatedAt,
    EventBadgeGuardianResponse? Guardian,
    IReadOnlyList<EventBadgeActivityResponse> Activities
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
/// Contains the event roster participant data returned by the API.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="FirstName">User's given name.</param>
/// <param name="LastName">User's family name.</param>
/// <param name="BirthDate">User's date of birth; only dependents have one.</param>
/// <param name="Email">Email address to validate or locate.</param>
/// <param name="Phone">Phone number to validate or locate.</param>
/// <param name="SecondaryPhone">Optional second contact phone of the participant.</param>
/// <param name="RoleName">The role name value.</param>
/// <param name="Guardian">The guardian value.</param>
public record EventRosterParticipantResponse(
    Guid UserId,
    string FirstName,
    string LastName,
    DateOnly? BirthDate,
    string? Email,
    string? Phone,
    string? SecondaryPhone,
    string RoleName,
    EventGuardianContactResponse? Guardian
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
/// <param name="News">The news value.</param>
/// <param name="Partners">The partners value.</param>
/// <param name="Users">The users value.</param>
public record DashboardSummaryResponse(
    int Events,
    int Activities,
    int Resources,
    int News,
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
/// Groups of a breakdown, each with one value per series, such as people per age group and gender.
/// </summary>
/// <param name="Groups">Keys of the groups, in display order.</param>
/// <param name="Series">One series per key, with a value per group in the order of <paramref name="Groups"/>.</param>
public record DashboardBreakdownResponse(
    IReadOnlyList<string> Groups,
    IReadOnlyList<DashboardSeriesResponse> Series
);

/// <summary>
/// An event of the ranking of confirmed signups in the activities of the range.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="Title">The title value.</param>
/// <param name="Confirmed">Confirmed signups in its activities of the range, in any role.</param>
/// <param name="SeatsFilled">Seats those activities filled, counting each role up to its desired number.</param>
/// <param name="SeatsDesired">Seats those activities wanted, summed over the roles with a desired number.</param>
public record DashboardTopEventResponse(
    Guid EventId,
    string Title,
    int Confirmed,
    int SeatsFilled,
    int SeatsDesired
);

/// <summary>
/// Signups of an event still waiting for a decision in its upcoming activities.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="Title">The title value.</param>
/// <param name="Pending">Signups requested and not yet confirmed or denied.</param>
/// <param name="OldestRequestedAt">When the oldest of them was requested.</param>
/// <param name="NextActivityStartsAt">Start of the soonest activity with one of them.</param>
public record DashboardPendingEventResponse(
    Guid EventId,
    string Title,
    int Pending,
    DateTimeOffset OldestRequestedAt,
    DateTimeOffset NextActivityStartsAt
);

/// <summary>
/// Signups waiting for a decision in upcoming activities, overall and per event.
/// </summary>
/// <param name="Total">Signups waiting across every upcoming activity.</param>
/// <param name="Events">Events with any of them, the soonest activity first.</param>
public record DashboardPendingSignupsResponse(
    int Total,
    IReadOnlyList<DashboardPendingEventResponse> Events
);

/// <summary>
/// Seats of one role filled versus wanted across the upcoming activities.
/// </summary>
/// <param name="Key">Role: <c>leader</c>, <c>volunteer</c> or <c>participant</c>.</param>
/// <param name="Confirmed">Seats filled by confirmed people, counting each activity up to its desired number.</param>
/// <param name="Desired">Seats wanted, summed over the activities that set a desired number for the role.</param>
public record DashboardRoleCoverageResponse(string Key, int Confirmed, int Desired);

/// <summary>
/// Mean score of one event that ended in the range.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="Title">The title value.</param>
/// <param name="Average">Mean of its scored ratings.</param>
/// <param name="Scored">Number of its ratings that carry a score.</param>
public record DashboardEventSatisfactionResponse(
    Guid EventId,
    string Title,
    double Average,
    int Scored
);

/// <summary>
/// Scores of the events that ended in the range.
/// </summary>
/// <param name="Average">Mean of every scored rating of those events, or <see langword="null"/> without any.</param>
/// <param name="Scored">Number of their ratings that carry a score.</param>
/// <param name="Events">The latest of those events with a score, the most recent first.</param>
public record DashboardSatisfactionResponse(
    double? Average,
    int Scored,
    IReadOnlyList<DashboardEventSatisfactionResponse> Events
);

/// <summary>
/// Contains the dashboard occupancy activity data returned by the API.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="Title">The title value.</param>
/// <param name="StartsAt">The starts at value.</param>
/// <param name="Confirmed">Seats filled by confirmed people, counting each role up to its desired number.</param>
/// <param name="Desired">Seats wanted, summed over the roles with a desired number.</param>
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
/// <param name="Confirmed">Seats filled across its activities, never above each role's desired number.</param>
/// <param name="Desired">Seats wanted across its activities.</param>
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
/// <param name="Confirmed">Seats filled across every upcoming activity, never above each role's desired number.</param>
/// <param name="Desired">Seats wanted across every upcoming activity.</param>
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
/// <param name="NewUsers">Accounts created in each bucket, per membership type.</param>
/// <param name="Inscriptions">The inscriptions value.</param>
/// <param name="TopEvents">Events with the most confirmed signups in their activities of the range.</param>
/// <param name="PendingSignups">Signups waiting for a decision in upcoming activities, whatever the range.</param>
/// <param name="RoleCoverage">Seats filled versus wanted per role in upcoming activities, whatever the range.</param>
/// <param name="ParticipantProfile">Distinct people confirmed in activities of the range, per age group (on the last day of the range) and gender.</param>
/// <param name="CategoryDemand">Confirmed signups in activities of the range per event category, the busiest first.</param>
/// <param name="Recurrence">Distinct people confirmed in activities of the range, by how many events they joined.</param>
/// <param name="Satisfaction">Scores of the events that ended in the range.</param>
/// <param name="Occupancy">The occupancy value.</param>
public record DashboardAnalyticsResponse(
    DateOnly RangeStart,
    DateOnly RangeEnd,
    string Granularity,
    IReadOnlyList<DashboardKpiResponse> Kpis,
    DashboardTimeSeriesResponse NewUsers,
    DashboardTimeSeriesResponse Inscriptions,
    IReadOnlyList<DashboardTopEventResponse> TopEvents,
    DashboardPendingSignupsResponse PendingSignups,
    IReadOnlyList<DashboardRoleCoverageResponse> RoleCoverage,
    DashboardBreakdownResponse ParticipantProfile,
    IReadOnlyList<DashboardSliceResponse> CategoryDemand,
    IReadOnlyList<DashboardSliceResponse> Recurrence,
    DashboardSatisfactionResponse Satisfaction,
    DashboardOccupancyResponse Occupancy
);
