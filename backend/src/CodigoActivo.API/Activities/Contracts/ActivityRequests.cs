using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using EventId = CodigoActivo.Domain.Events.EventId;

namespace CodigoActivo.API.Activities.Contracts;

/// <summary>
/// Contains the client-supplied data used to activity role capacity.
/// </summary>
/// <param name="ActivityRoleTypeId">Identifier of the activity role type.</param>
/// <param name="DesiredCount">Number of desired allowed or reported.</param>
public record ActivityRoleCapacityRequest(
    [Required] Guid ActivityRoleTypeId,
    [Required] [Range(1, 10000)] int? DesiredCount
);

/// <summary>
/// Contains the client-supplied data used to create an activity.
/// </summary>
/// <param name="Title">The title value.</param>
/// <param name="Description">The description value.</param>
/// <param name="Location">The location value.</param>
/// <param name="ActivityModalityTypeId">Identifier of the activity modality type.</param>
/// <param name="ActivityStartsAt">The activity starts at value.</param>
/// <param name="ActivityEndsAt">The activity ends at value.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
/// <param name="RoleCapacities">The role capacities value.</param>
public record CreateActivityRequest(
    [Required] [MaxLength(200)] string Title,
    [Required] [MaxLength(4000)] string Description,
    [Required] [MaxLength(200)] string Location,
    [Required] Guid ActivityModalityTypeId,
    [Required] DateTimeOffset? ActivityStartsAt,
    [Required] DateTimeOffset? ActivityEndsAt,
    Guid ThumbnailId,
    IReadOnlyList<ActivityRoleCapacityRequest>? RoleCapacities
)
{
    /// <summary>
    /// Builds the command that creates the activity.
    /// </summary>
    /// <param name="eventId">Identifier of the event the activity belongs to.</param>
    /// <returns>The command.</returns>
    public CreateActivityCommand ToCommand(EventId eventId)
    {
        return new CreateActivityCommand(
            eventId,
            ActivityRequestMapping.Draft(
                Title,
                Description,
                Location,
                ActivityModalityTypeId,
                ActivityStartsAt,
                ActivityEndsAt,
                ThumbnailId,
                RoleCapacities
            )
        );
    }
}

/// <summary>
/// Contains the client-supplied data used to update the activity.
/// </summary>
/// <param name="Title">The title value.</param>
/// <param name="Description">The description value.</param>
/// <param name="Location">The location value.</param>
/// <param name="ActivityModalityTypeId">Identifier of the activity modality type.</param>
/// <param name="ActivityStartsAt">The activity starts at value.</param>
/// <param name="ActivityEndsAt">The activity ends at value.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
/// <param name="RoleCapacities">The role capacities value.</param>
public record UpdateActivityRequest(
    [Required] [MaxLength(200)] string Title,
    [Required] [MaxLength(4000)] string Description,
    [Required] [MaxLength(200)] string Location,
    [Required] Guid ActivityModalityTypeId,
    [Required] DateTimeOffset? ActivityStartsAt,
    [Required] DateTimeOffset? ActivityEndsAt,
    Guid ThumbnailId,
    IReadOnlyList<ActivityRoleCapacityRequest>? RoleCapacities
)
{
    /// <summary>
    /// Builds the command that updates the activity.
    /// </summary>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <returns>The command.</returns>
    public UpdateActivityCommand ToCommand(ActivityId activityId)
    {
        return new UpdateActivityCommand(
            activityId,
            ActivityRequestMapping.Draft(
                Title,
                Description,
                Location,
                ActivityModalityTypeId,
                ActivityStartsAt,
                ActivityEndsAt,
                ThumbnailId,
                RoleCapacities
            )
        );
    }
}

/// <summary>
/// Contains the client-supplied decision for a single terms document during a signup.
/// </summary>
/// <param name="TermsDocumentId">Identifier of the terms document.</param>
/// <param name="Accepted">Whether the user accepted the document.</param>
public record TermsDecisionRequest([Required] Guid TermsDocumentId, [Required] bool? Accepted);

/// <summary>
/// Contains the client-supplied data used to assign.
/// </summary>
/// <param name="ActivityRoleTypeId">Identifier of the activity role type.</param>
/// <param name="TermsDecisions">The terms decisions supplied for the event's linked documents.</param>
public record AssignRequest(
    [Required] Guid ActivityRoleTypeId,
    IReadOnlyList<TermsDecisionRequest>? TermsDecisions = null
)
{
    /// <summary>
    /// Builds the command that signs the person up.
    /// </summary>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="userId">Identifier of the person signed up.</param>
    /// <returns>The command.</returns>
    public AssignActivityCommand ToCommand(ActivityId activityId, UserId userId)
    {
        return new AssignActivityCommand(
            activityId,
            userId,
            ActivityRoleTypeId,
            ActivityRequestMapping.Decisions(TermsDecisions)
        );
    }
}

/// <summary>
/// Contains the client-supplied data used to assign household.
/// </summary>
/// <param name="Assignments">The assignments value.</param>
/// <param name="TermsDecisions">The terms decisions supplied for the event's linked documents.</param>
public record AssignHouseholdRequest(
    [Required]
    [MaxLength(Household.MaxMembers)]
        IReadOnlyList<HouseholdAssignmentRequest> Assignments,
    IReadOnlyList<TermsDecisionRequest>? TermsDecisions = null
)
{
    /// <summary>
    /// Builds the command that signs the household members up.
    /// </summary>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <returns>The command.</returns>
    public AssignHouseholdCommand ToCommand(ActivityId activityId)
    {
        return new AssignHouseholdCommand(
            activityId,
            [
                .. (Assignments ?? []).Select(assignment => new HouseholdMemberSignup(
                    UserId.From(assignment.UserId),
                    assignment.ActivityRoleTypeId
                )),
            ],
            ActivityRequestMapping.Decisions(TermsDecisions)
        );
    }
}

/// <summary>
/// Contains the client-supplied data used to household assignment.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="ActivityRoleTypeId">Identifier of the activity role type.</param>
public record HouseholdAssignmentRequest(
    [Required] Guid UserId,
    [Required] Guid ActivityRoleTypeId
);

/// <summary>
/// Contains the client-supplied data used to change assignment status.
/// </summary>
/// <param name="AssignmentStatusId">Identifier of the assignment status.</param>
public record ChangeAssignmentStatusRequest([Required] Guid AssignmentStatusId);

/// <summary>
/// Contains the client-supplied data used to change assignment role.
/// </summary>
/// <param name="ActivityRoleTypeId">Identifier of the activity role type.</param>
public record ChangeAssignmentRoleRequest([Required] Guid ActivityRoleTypeId);

internal static class ActivityRequestMapping
{
    public static ActivityDraft Draft(
        string title,
        string description,
        string location,
        Guid modalityTypeId,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt,
        Guid thumbnailId,
        IReadOnlyList<ActivityRoleCapacityRequest>? roleCapacities
    )
    {
        return new ActivityDraft(
            title,
            description,
            location,
            modalityTypeId,
            startsAt,
            endsAt,
            StoredFileId.From(thumbnailId),
            [
                .. (roleCapacities ?? []).Select(capacity => new RoleCapacityDraft(
                    capacity.ActivityRoleTypeId,
                    capacity.DesiredCount.GetValueOrDefault()
                )),
            ]
        );
    }

    public static IReadOnlyList<TermsDecision>? Decisions(
        IReadOnlyList<TermsDecisionRequest>? decisions
    )
    {
        return decisions
            ?.Select(decision => new TermsDecision(
                TermsDocumentId.From(decision.TermsDocumentId),
                decision.Accepted ?? false
            ))
            .ToList();
    }
}
