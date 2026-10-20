using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.TermsDecisions;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Activities.Commands;

/// <summary>
/// Everything an activity is made of when it is created or replaced.
/// </summary>
/// <param name="Title">Title.</param>
/// <param name="Description">Plain-text description.</param>
/// <param name="Location">Where it takes place.</param>
/// <param name="Modality">How people attend it.</param>
/// <param name="ActivityStartsAt">When it starts.</param>
/// <param name="ActivityEndsAt">When it ends.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail file.</param>
/// <param name="RoleCapacities">How many people each role takes.</param>
public sealed record ActivityDraft(
    [property: Required, MaxLength(200), NotBlank] string Title,
    [property: Required, MaxLength(4000), NotBlank] string Description,
    [property: Required, MaxLength(200), NotBlank] string Location,
    ActivityModality Modality,
    DateTimeOffset? ActivityStartsAt,
    DateTimeOffset? ActivityEndsAt,
    StoredFileId ThumbnailId,
    [property: MaxLength(50)] IReadOnlyList<RoleCapacityDraft> RoleCapacities
)
{
    /// <summary>
    /// Gets the details the activity is created or replaced with.
    /// </summary>
    /// <returns>The details.</returns>
    public ActivityDetails ToDetails()
    {
        return new ActivityDetails(Title, Description, Location, Modality, ThumbnailId);
    }
}

/// <summary>
/// How many people a role of an activity takes.
/// </summary>
/// <param name="Role">Role.</param>
/// <param name="DesiredCount">Number of people wanted in the role.</param>
public sealed record RoleCapacityDraft(
    ActivityRole Role,
    [property: Range(RoleCapacity.MinDesiredCount, RoleCapacity.MaxDesiredCount)] int DesiredCount
);

/// <summary>
/// A household member to sign up, the role they take and the terms decisions taken for them.
/// </summary>
/// <param name="UserId">Identifier of the household member.</param>
/// <param name="Role">Role the member takes.</param>
/// <param name="TermsDecisions">
/// Decisions on the terms documents of the event for the member, if any were asked.
/// </param>
public sealed record HouseholdMemberSignup(
    UserId UserId,
    ActivityRole Role,
    [property: MaxLength(50)] IReadOnlyList<TermsDecision>? TermsDecisions = null
);
