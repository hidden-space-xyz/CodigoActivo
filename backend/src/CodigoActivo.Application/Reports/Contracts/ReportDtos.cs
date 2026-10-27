using CodigoActivo.Application.Abstractions.Querying.ReadModel;

namespace CodigoActivo.Application.Reports.Contracts;

/// <summary>
/// Contains the summary of the signups and ratings of an event, returned by the API.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="Title">Title of the event.</param>
/// <param name="ActivitiesCount">Number of activities of the event.</param>
/// <param name="TotalAssignments">Number of signups to its activities, any status.</param>
/// <param name="RequestedAssignments">Number of signups waiting for a decision.</param>
/// <param name="ConfirmedAssignments">Number of confirmed signups.</param>
/// <param name="DeniedAssignments">Number of denied signups.</param>
/// <param name="DistinctVolunteers">
/// Number of different people signed up to any activity, whatever their role or status.
/// </param>
/// <param name="RatingsCount">Number of ratings received, with or without a score.</param>
/// <param name="ScoredRatingsCount">Number of ratings that carry a score, the ones the average covers.</param>
/// <param name="RatingsAverage">Mean score of the scored ratings, or <see langword="null"/> without any.</param>
/// <param name="RoleTypeBreakdown">Confirmed signups per role, every role listed by name.</param>
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
/// Contains the confirmed signups of one role in an event, returned by the API.
/// </summary>
/// <param name="RoleTypeId">Catalog identifier of the role.</param>
/// <param name="RoleTypeName">Display name of the role.</param>
/// <param name="ApprovedAssignments">Number of confirmed signups in the role.</param>
public record EventRoleTypeSummaryResponse(
    Guid RoleTypeId,
    string? RoleTypeName,
    int ApprovedAssignments
);

/// <summary>
/// Contains one signup of an attendee in the attendee list, returned by the API.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="ActivityTitle">Title of the activity.</param>
/// <param name="ActivityStartsAt">When the activity starts, in UTC.</param>
/// <param name="ActivityEndsAt">When the activity ends, in UTC.</param>
/// <param name="RoleTypeId">Catalog identifier of the role taken.</param>
/// <param name="RoleTypeName">Display name of the role.</param>
/// <param name="StatusId">Catalog identifier of the review status.</param>
/// <param name="StatusName">Display name of the status.</param>
/// <param name="SignedUpAt">UTC timestamp of the signup.</param>
/// <param name="HasTimeConflict">
/// Whether the signup, unless denied, overlaps in time another signup of the same person to the
/// event that is not denied.
/// </param>
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
/// Contains a person in the attendee list of an event, returned by the API.
/// </summary>
/// <param name="UserId">Identifier of the attendee.</param>
/// <param name="FirstName">Attendee's given name.</param>
/// <param name="LastName">Attendee's family name.</param>
/// <param name="Email">Attendee's email address; dependents have none.</param>
/// <param name="Phone">Attendee's primary phone; dependents have none.</param>
/// <param name="SecondaryPhone">Optional second contact phone of the attendee.</param>
/// <param name="BirthDate">User's date of birth; only dependents have one.</param>
/// <param name="Gender">Gender of the attendee.</param>
/// <param name="UserTypeName">Display name of the membership type.</param>
/// <param name="UserTypeColor">Color the membership type is shown with.</param>
/// <param name="Guardian">Guardian of a dependent; <see langword="null"/> for anyone else.</param>
/// <param name="Assignments">
/// Signups of the attendee to the event that match the criteria, ordered by activity start.
/// </param>
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
/// Contains the guardian printed on the badge of a dependent: names and phone only.
/// </summary>
/// <param name="FirstName">Guardian's given name.</param>
/// <param name="LastName">Guardian's family name.</param>
/// <param name="Phone">Guardian's primary phone.</param>
public record EventBadgeGuardianResponse(string FirstName, string LastName, string? Phone);

/// <summary>
/// Contains one confirmed activity printed on an event badge, without its modality.
/// </summary>
/// <param name="Title">Title of the activity.</param>
/// <param name="Location">Concrete place or platform where the activity takes place.</param>
public record EventBadgeActivityResponse(string Title, string Location);

/// <summary>
/// Contains the badge of a person with a confirmed signup to an event, returned by the API.
/// </summary>
/// <param name="UserId">Identifier of the person.</param>
/// <param name="FirstName">Person's given name.</param>
/// <param name="LastName">Person's family name.</param>
/// <param name="UserTypeName">Display name of the membership type.</param>
/// <param name="UserTypeColor">Color the membership type is shown with.</param>
/// <param name="CreatedAt">UTC timestamp when the person's account was created.</param>
/// <param name="Guardian">Guardian of a dependent; <see langword="null"/> for anyone else.</param>
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
/// Contains the badges of an event, returned by the API.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="Title">Title of the event.</param>
/// <param name="Badges">One badge per person confirmed, ordered by last and first name.</param>
public record EventBadgesResponse(
    Guid EventId,
    string Title,
    IReadOnlyList<EventBadgeResponse> Badges
);

/// <summary>
/// Contains a person confirmed in an activity of the event roster, returned by the API.
/// </summary>
/// <param name="UserId">Identifier of the person.</param>
/// <param name="FirstName">Person's given name.</param>
/// <param name="LastName">Person's family name.</param>
/// <param name="BirthDate">User's date of birth; only dependents have one.</param>
/// <param name="Email">Person's email address; dependents have none.</param>
/// <param name="Phone">Person's primary phone; dependents have none.</param>
/// <param name="SecondaryPhone">Optional second contact phone of the participant.</param>
/// <param name="RoleName">Display name of the role the person is confirmed in.</param>
/// <param name="Guardian">Guardian of a dependent; <see langword="null"/> for anyone else.</param>
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
/// Contains an activity of the event roster with the people confirmed in it, returned by the API.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="Title">Title of the activity.</param>
/// <param name="Location">Where the activity takes place.</param>
/// <param name="ActivityStartsAt">When it starts, in UTC.</param>
/// <param name="ActivityEndsAt">When it ends, in UTC.</param>
/// <param name="Participants">
/// People confirmed in it, by role (leaders, volunteers, then participants) and then by name.
/// </param>
public record EventRosterActivityResponse(
    Guid ActivityId,
    string Title,
    string Location,
    DateTimeOffset ActivityStartsAt,
    DateTimeOffset ActivityEndsAt,
    IReadOnlyList<EventRosterParticipantResponse> Participants
);

/// <summary>
/// Contains the roster of an event, returned by the API.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="Title">Title of the event.</param>
/// <param name="Activities">
/// Activities with at least one confirmed signup, in chronological order.
/// </param>
public record EventRosterResponse(
    Guid EventId,
    string Title,
    IReadOnlyList<EventRosterActivityResponse> Activities
);

/// <summary>
/// Contains the totals of the dashboard summary, returned by the API.
/// </summary>
/// <param name="Events">Number of events.</param>
/// <param name="Activities">Number of activities.</param>
/// <param name="Resources">Number of resources.</param>
/// <param name="News">Number of news items.</param>
/// <param name="Partners">Number of partners.</param>
/// <param name="Users">Number of accounts, dependents included.</param>
public record DashboardSummaryResponse(
    int Events,
    int Activities,
    int Resources,
    int News,
    int Partners,
    int Users
);

/// <summary>
/// Contains how many rows of one source the dashboard counts, returned by the API.
/// </summary>
/// <param name="Key">
/// Source: <c>users</c>, <c>members</c>, <c>inscriptions</c>, <c>events</c>, <c>activities</c>,
/// <c>resources</c>, <c>news</c> or <c>partners</c>.
/// </param>
/// <param name="Total">Rows that exist now.</param>
/// <param name="InRange">Rows created within the range.</param>
/// <param name="PreviousRange">Rows created in the period of equal length just before it.</param>
public record DashboardKpiResponse(string Key, int Total, int InRange, int PreviousRange);

/// <summary>
/// Contains one series of a chart, returned by the API.
/// </summary>
/// <param name="Key">Key of the series, such as a membership type, a status or a gender.</param>
/// <param name="Values">One value per bucket or group of the chart, in its order.</param>
public record DashboardSeriesResponse(string Key, IReadOnlyList<int> Values);

/// <summary>
/// Contains a chart of what was created per bucket of the range, returned by the API.
/// </summary>
/// <param name="Buckets">First day of every bucket, in order.</param>
/// <param name="Series">Series with one value per bucket.</param>
public record DashboardTimeSeriesResponse(
    IReadOnlyList<DateOnly> Buckets,
    IReadOnlyList<DashboardSeriesResponse> Series
);

/// <summary>
/// Contains one slice of a distribution, returned by the API.
/// </summary>
/// <param name="Key">Key of the slice, such as a category identifier or a recurrence band.</param>
/// <param name="Label">Name to show, or <see langword="null"/> when the key is shown as is.</param>
/// <param name="Color">Color to show it with, or <see langword="null"/> for the default.</param>
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
/// <param name="Title">Title of the event.</param>
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
/// <param name="Title">Title of the event.</param>
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
/// <param name="Title">Title of the event.</param>
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
/// <param name="Events">
/// Up to eight of those events with a score, the most recently ended first.
/// </param>
public record DashboardSatisfactionResponse(
    double? Average,
    int Scored,
    IReadOnlyList<DashboardEventSatisfactionResponse> Events
);

/// <summary>
/// Contains the seats of an upcoming activity with a desired number of people, returned by the API.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="Title">Title of the activity.</param>
/// <param name="StartsAt">When it starts, in UTC.</param>
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
/// Contains the seats of the upcoming activities of an event, returned by the API.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="Title">Title of the event.</param>
/// <param name="Confirmed">Seats filled across its activities, never above each role's desired number.</param>
/// <param name="Desired">Seats wanted across its activities.</param>
/// <param name="Activities">
/// Its upcoming activities with a desired number, in chronological order.
/// </param>
public record DashboardOccupancyEventResponse(
    Guid EventId,
    string Title,
    int Confirmed,
    int Desired,
    IReadOnlyList<DashboardOccupancyActivityResponse> Activities
);

/// <summary>
/// Contains the seats filled versus wanted in the upcoming activities, whatever the range, returned
/// by the API.
/// </summary>
/// <param name="Confirmed">Seats filled across every upcoming activity, never above each role's desired number.</param>
/// <param name="Desired">Seats wanted across every upcoming activity.</param>
/// <param name="Events">Events with such activities, the one with the soonest first.</param>
public record DashboardOccupancyResponse(
    int Confirmed,
    int Desired,
    IReadOnlyList<DashboardOccupancyEventResponse> Events
);

/// <summary>
/// Contains the dashboard analytics data returned by the API.
/// </summary>
/// <param name="RangeStart">First local day of the range.</param>
/// <param name="RangeEnd">Last local day of the range.</param>
/// <param name="Granularity">
/// Bucket size of the time series: <c>day</c>, <c>week</c> or <c>month</c>.
/// </param>
/// <param name="Kpis">
/// Totals of each source, created in the range and in the period before it.
/// </param>
/// <param name="NewUsers">Accounts created in each bucket, per membership type.</param>
/// <param name="Inscriptions">Signups created in each bucket, per current status.</param>
/// <param name="TopEvents">
/// Up to eight events with the most confirmed signups in their activities of the range.
/// </param>
/// <param name="PendingSignups">Signups waiting for a decision in upcoming activities, whatever the range.</param>
/// <param name="RoleCoverage">Seats filled versus wanted per role in upcoming activities, whatever the range.</param>
/// <param name="ParticipantProfile">Distinct people confirmed in activities of the range, per age group (on the last day of the range) and gender.</param>
/// <param name="CategoryDemand">Confirmed signups in activities of the range per event category, the busiest first.</param>
/// <param name="Recurrence">Distinct people confirmed in activities of the range, by how many events they joined.</param>
/// <param name="Satisfaction">Scores of the events that ended in the range.</param>
/// <param name="Occupancy">
/// Seats filled versus wanted in upcoming activities, whatever the range.
/// </param>
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
