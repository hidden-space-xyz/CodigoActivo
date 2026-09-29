using System.Linq.Expressions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Activities;

/// <summary>
/// Database projections from the read models to the response shapes of this feature.
/// </summary>
public static class ActivityProjections
{
    /// <summary>
    /// Stores the shared activity value.
    /// </summary>
    public static readonly Expression<Func<ActivityRow, ActivityResponse>> Activity =
        activity => new ActivityResponse
        {
            Id = activity.Id,
            Title = activity.Title,
            Description = activity.Description,
            Location = activity.Location,
            ActivityStartsAt = activity.ActivityStartsAt,
            ActivityEndsAt = activity.ActivityEndsAt,
            EventId = activity.EventId,
            ModalityId = activity.ActivityModalityTypeId,
            ModalityName = activity.ActivityModalityType.Name,
            ThumbnailId = activity.ThumbnailId,
            RoleCapacities = activity
                .RoleCapacities.OrderBy(capacity => capacity.ActivityRoleTypeId)
                .Select(capacity => new ActivityRoleCapacityResponse(
                    capacity.ActivityRoleTypeId,
                    capacity.DesiredCount,
                    activity
                        .Assignments.Where(assignment =>
                            assignment.ActivityRoleTypeId == capacity.ActivityRoleTypeId
                            && assignment.AssignmentStatusId != SeedIds.AssignmentStatusTypes.Denied
                        )
                        .Skip(capacity.DesiredCount)
                        .Any()
                ))
                .ToList(),
            CreatedAt = activity.CreatedAt,
            UpdatedAt = activity.UpdatedAt,
            CreatedBy = activity.CreatedBy,
            UpdatedBy = activity.UpdatedBy,
        };

    /// <summary>
    /// Stores the shared activity role type value.
    /// </summary>
    public static readonly Expression<
        Func<ActivityRoleTypeRow, ActivityRoleTypeResponse>
    > ActivityRoleType = roleType => new ActivityRoleTypeResponse
    {
        Id = roleType.Id,
        Name = roleType.Name,
        Description = roleType.Description,
    };

    /// <summary>
    /// Stores the shared assignment status type value.
    /// </summary>
    public static readonly Expression<
        Func<AssignmentStatusTypeRow, AssignmentStatusTypeResponse>
    > AssignmentStatusType = statusType => new AssignmentStatusTypeResponse
    {
        Id = statusType.Id,
        Name = statusType.Name,
        Description = statusType.Description,
        Color = statusType.Color,
    };

    /// <summary>
    /// Stores the shared activity modality type value.
    /// </summary>
    public static readonly Expression<
        Func<ActivityModalityTypeRow, ActivityModalityTypeResponse>
    > ActivityModalityType = modalityType => new ActivityModalityTypeResponse
    {
        Id = modalityType.Id,
        Name = modalityType.Name,
    };

    /// <summary>
    /// Projects an assignment to the response returned after signing up or changing it.
    /// </summary>
    public static readonly Expression<Func<AssignmentRow, AssignmentResponse>> Assignment =
        assignment => new AssignmentResponse(
            assignment.UserId,
            assignment.ActivityId,
            assignment.ActivityRoleTypeId,
            assignment.ActivityRoleType.Name,
            new AssignmentStatusResponse(
                assignment.AssignmentStatusId,
                assignment.AssignmentStatus.Name
            )
        );

    /// <summary>
    /// Stores the shared assigned activity value.
    /// </summary>
    public static readonly Expression<
        Func<AssignmentRow, AssignedActivityResponse>
    > AssignedActivity = assignment => new AssignedActivityResponse
    {
        ActivityId = assignment.ActivityId,
        Title = assignment.Activity.Title,
        Description = assignment.Activity.Description,
        ActivityStartsAt = assignment.Activity.ActivityStartsAt,
        ActivityEndsAt = assignment.Activity.ActivityEndsAt,
        EventId = assignment.Activity.EventId,
        RoleType = new AssignedActivityRoleResponse(
            assignment.ActivityRoleTypeId,
            assignment.ActivityRoleType.Name
        ),
        Status = new AssignedActivityStatusResponse(
            assignment.AssignmentStatusId,
            assignment.AssignmentStatus.Name
        ),
    };
}
