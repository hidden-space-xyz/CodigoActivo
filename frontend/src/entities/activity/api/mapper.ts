import type {
  ActivityModalityTypeResponse,
  ActivityResponse,
  ActivityRoleTypeResponse,
  AssignedActivityResponse,
  AssignmentStatusTypeResponse,
  CreateActivityRequest,
  HouseholdMemberAssignmentResponse,
  HouseholdSignupRolesResponse,
  TimeOverlapResponse,
  UserResponse,
} from '@/shared/api/generated/models'

import type {
  ActivityAssignment,
  ActivityDetail,
  ActivityInput,
  ActivityListing,
  ActivityModality,
  ActivityRole,
  AssignmentStatus,
  EventActivity,
  HouseholdActivityAssignment,
  HouseholdMember,
  HouseholdSignupRoles,
  OverlapCheck,
} from '../model/types'

/** Maps an activity for the public event timeline, keeping the ids of its high-demand roles. */
export function toEventActivity(activity: ActivityResponse): EventActivity {
  return {
    id: activity.id,
    title: activity.title,
    description: activity.description,
    location: activity.location,
    modality: activity.modalityName,
    startsAt: activity.activityStartsAt,
    endsAt: activity.activityEndsAt,
    highDemandRoleIds: activity.roleCapacities
      .filter((capacity) => capacity.isHighDemand)
      .map((capacity) => capacity.activityRoleTypeId),
  }
}

/** Maps an activity for the admin edit form. */
export function toActivityDetail(activity: ActivityResponse): ActivityDetail {
  return {
    id: activity.id,
    title: activity.title,
    description: activity.description,
    location: activity.location,
    modalityId: activity.modalityId,
    startsAt: activity.activityStartsAt,
    endsAt: activity.activityEndsAt,
    thumbnailId: activity.thumbnailId,
    roleCapacities: activity.roleCapacities.map((capacity) => ({
      roleTypeId: capacity.activityRoleTypeId,
      desiredCount: capacity.desiredCount,
    })),
  }
}

/** Maps an activity for the admin activities table. */
export function toActivityListing(activity: ActivityResponse): ActivityListing {
  return {
    id: activity.id,
    title: activity.title,
    location: activity.location,
    modality: activity.modalityName,
    startsAt: activity.activityStartsAt,
    endsAt: activity.activityEndsAt,
    thumbnailId: activity.thumbnailId,
  }
}

/**
 * Builds the body that creates or replaces an activity; both endpoints take the same fields and
 * an activity without desired counts sends none.
 */
export function toActivityRequest(input: ActivityInput): CreateActivityRequest {
  return {
    title: input.title,
    description: input.description,
    location: input.location,
    activityModalityTypeId: input.modalityId,
    activityStartsAt: input.startsAt,
    activityEndsAt: input.endsAt,
    thumbnailId: input.thumbnailId,
    roleCapacities: input.roleCapacities.length
      ? input.roleCapacities.map((capacity) => ({
          activityRoleTypeId: capacity.roleTypeId,
          desiredCount: capacity.desiredCount,
        }))
      : null,
  }
}

/** Maps a role of the activity roles catalog. */
export function toActivityRole(role: ActivityRoleTypeResponse): ActivityRole {
  return { id: role.id, name: role.name }
}

/** Maps a status of the signup statuses catalog. */
export function toAssignmentStatus(status: AssignmentStatusTypeResponse): AssignmentStatus {
  return { id: status.id, name: status.name, color: status.color }
}

/** Maps a modality of the activity modalities catalog. */
export function toActivityModality(modality: ActivityModalityTypeResponse): ActivityModality {
  return { id: modality.id, name: modality.name }
}

/** Maps the roles a household member may sign up for. */
export function toHouseholdSignupRoles(item: HouseholdSignupRolesResponse): HouseholdSignupRoles {
  return {
    userId: item.userId,
    roles: item.roles.map((role) => ({ id: role.id, name: role.name })),
  }
}

/** Maps one of the signed-in user's own signups. */
export function toActivityAssignment(assignment: AssignedActivityResponse): ActivityAssignment {
  return {
    activityId: assignment.activityId,
    status: assignment.status.name,
    roleName: assignment.roleType.name,
  }
}

/** Maps a household member's signup, joining first and last name into `name`. */
export function toHouseholdActivityAssignment(
  assignment: HouseholdMemberAssignmentResponse,
): HouseholdActivityAssignment {
  return {
    activityId: assignment.activityId,
    userId: assignment.userId,
    name: `${assignment.firstName} ${assignment.lastName}`.trim(),
    roleName: assignment.roleName,
    status: assignment.statusName,
  }
}

/** Reduces a minor to the id and full name used by signup pickers. */
export function toHouseholdMember(child: UserResponse): HouseholdMember {
  return { id: child.id, name: `${child.firstName} ${child.lastName}`.trim() }
}

/** Maps the schedule-conflict check run before signing a user up for an activity. */
export function toOverlapCheck(overlap: TimeOverlapResponse): OverlapCheck {
  return {
    hasOverlaps: overlap.hasOverlaps,
    overlaps: overlap.overlaps.map((item) => ({
      activityId: item.activityId,
      title: item.title,
      startsAt: item.startsAt,
      endsAt: item.endsAt,
    })),
  }
}
