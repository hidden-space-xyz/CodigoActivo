namespace CodigoActivo.Domain.Users;

/// <summary>
/// Signup of a member of the household to an activity, as the legal copy keeps it. Titles and
/// catalog names are copied by value.
/// </summary>
/// <param name="Id">Identifier of the activity.</param>
/// <param name="Title">Title of the activity.</param>
/// <param name="Location">Location of the activity.</param>
/// <param name="Modality">Name of the modality.</param>
/// <param name="StartsAt">When the activity starts.</param>
/// <param name="EndsAt">When the activity ends.</param>
/// <param name="ParticipantId">Identifier of the member signed up.</param>
/// <param name="Role">Name of the role.</param>
/// <param name="Status">Name of the signup status.</param>
/// <param name="SignedUpAt">When the member signed up.</param>
public sealed record LegalCopyActivity(
    Guid Id,
    string Title,
    string Location,
    string Modality,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    Guid ParticipantId,
    string Role,
    string Status,
    DateTimeOffset SignedUpAt
);
