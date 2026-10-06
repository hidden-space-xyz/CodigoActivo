using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Activities.Commands;

/// <summary>
/// Everything an activity is made of when it is created or replaced.
/// </summary>
/// <param name="Title">Title.</param>
/// <param name="Description">Plain-text description.</param>
/// <param name="Location">Where it takes place.</param>
/// <param name="ActivityModalityTypeId">Catalog identifier of the modality.</param>
/// <param name="ActivityStartsAt">When it starts.</param>
/// <param name="ActivityEndsAt">When it ends.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail file.</param>
/// <param name="RoleCapacities">How many people each role takes.</param>
public sealed record ActivityDraft(
    [property: Required, MaxLength(200), NotBlank] string Title,
    [property: Required, MaxLength(4000), NotBlank] string Description,
    [property: Required, MaxLength(200), NotBlank] string Location,
    Guid ActivityModalityTypeId,
    DateTimeOffset? ActivityStartsAt,
    DateTimeOffset? ActivityEndsAt,
    StoredFileId ThumbnailId,
    IReadOnlyList<RoleCapacityDraft> RoleCapacities
);

/// <summary>
/// How many people a role of an activity takes.
/// </summary>
/// <param name="ActivityRoleTypeId">Catalog identifier of the role.</param>
/// <param name="DesiredCount">Number of people wanted in the role.</param>
public sealed record RoleCapacityDraft(
    Guid ActivityRoleTypeId,
    [property: Range(1, 10000)] int DesiredCount
);

/// <summary>
/// A household member to sign up and the role they take.
/// </summary>
/// <param name="UserId">Identifier of the household member.</param>
/// <param name="ActivityRoleTypeId">Catalog identifier of the role.</param>
public sealed record HouseholdMemberSignup(UserId UserId, Guid ActivityRoleTypeId);
