namespace CodigoActivo.Application.Activities;

/// <summary>
/// Describes the activity a signup decision is about, as the decision email presents it.
/// </summary>
/// <param name="ActivityTitle">Title of the activity.</param>
/// <param name="EventTitle">Title of the event the activity belongs to.</param>
/// <param name="EventId">Identifier of the event, used to link to it.</param>
/// <param name="Location">Where the activity takes place.</param>
/// <param name="StartsAt">When the activity starts.</param>
/// <param name="EndsAt">When the activity ends.</param>
public sealed record SignupActivity(
    string ActivityTitle,
    string EventTitle,
    Guid EventId,
    string Location,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt
);
