namespace CodigoActivo.Application.Activities.Contracts;

/// <summary>
/// Contains an activity that the requesting user leads with a confirmed assignment, with the
/// attendees currently confirmed in it grouped by role, returned by the API.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="Title">The title value.</param>
/// <param name="Location">Concrete place or platform where the activity takes place.</param>
/// <param name="ActivityStartsAt">The activity starts at value.</param>
/// <param name="ActivityEndsAt">The activity ends at value.</param>
/// <param name="Roles">Confirmed attendees grouped by role: leaders, volunteers, then participants.</param>
public record LeaderRosterActivityResponse(
    Guid ActivityId,
    string Title,
    string Location,
    DateTimeOffset ActivityStartsAt,
    DateTimeOffset ActivityEndsAt,
    IReadOnlyList<LeaderRosterRoleResponse> Roles
);

/// <summary>
/// Contains the confirmed attendees of one role in a led activity, split between independent
/// accounts and dependents because each kind carries different data, returned by the API.
/// </summary>
/// <param name="RoleTypeId">Identifier of the role type.</param>
/// <param name="RoleName">The role name value.</param>
/// <param name="Users">Attendees with an account of their own, reachable directly.</param>
/// <param name="Dependents">Minors signed up by their guardian, reachable through the guardian.</param>
public record LeaderRosterRoleResponse(
    Guid RoleTypeId,
    string RoleName,
    IReadOnlyList<LeaderRosterUserResponse> Users,
    IReadOnlyList<LeaderRosterDependentResponse> Dependents
);

/// <summary>
/// Contains what an activity leader may see about a confirmed attendee with an account of their
/// own, returned by the API.
/// </summary>
/// <param name="FirstName">Attendee's given name.</param>
/// <param name="LastName">Attendee's family name.</param>
/// <param name="Email">Attendee's email address.</param>
/// <param name="Phone">Attendee's primary phone.</param>
/// <param name="SignedUpAt">UTC timestamp when the attendee signed up for the activity.</param>
public record LeaderRosterUserResponse(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    DateTimeOffset SignedUpAt
);

/// <summary>
/// Contains what an activity leader may see about a confirmed dependent attendee, returned by the
/// API. The age is computed on the server for the day of the activity, so it neither reveals the
/// birth date nor changes while the list is available.
/// </summary>
/// <param name="FirstName">Dependent's given name.</param>
/// <param name="LastName">Dependent's family name.</param>
/// <param name="Age">Age in years on the local day the activity starts; <see langword="null"/> when no birth date is stored.</param>
/// <param name="Guardian">Guardian to contact about the dependent.</param>
/// <param name="SignedUpAt">UTC timestamp when the dependent was signed up for the activity.</param>
public record LeaderRosterDependentResponse(
    string FirstName,
    string LastName,
    int? Age,
    LeaderRosterGuardianResponse Guardian,
    DateTimeOffset SignedUpAt
);

/// <summary>
/// Contains the contact details of a dependent attendee's guardian shown to the activity leader,
/// returned by the API.
/// </summary>
/// <param name="FirstName">Guardian's given name.</param>
/// <param name="LastName">Guardian's family name.</param>
/// <param name="Email">Guardian's email address.</param>
/// <param name="Phone">Guardian's primary phone.</param>
public record LeaderRosterGuardianResponse(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone
);
