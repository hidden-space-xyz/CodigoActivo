import type { ActivityResponse, UserResponse } from '@/shared/api/generated/models'
import type {
  AssignedActivityResponse,
  HouseholdMemberAssignmentResponse,
  HouseholdSignupRolesResponse,
  TimeOverlapResponse,
} from '@/shared/api/generated/models'

import { i18n } from '@/shared/i18n'

import type {
  ActivityAssignment,
  ActivityDetail,
  ActivityOverlap,
  EventActivity,
  HouseholdActivityAssignment,
  HouseholdMember,
  HouseholdSignupRoles,
  OverlapCheck,
} from '../model/types'

/**
 * Maps an activity for the public event timeline, using a translated fallback title and keeping
 * only the ids of roles flagged as high demand.
 */
export function toEventActivity(activity: ActivityResponse): EventActivity {
  return {
    id: activity.id ?? '',
    title: activity.title ?? i18n.global.t('entities.activity.fallback.title'),
    description: activity.description ?? '',
    location: activity.location ?? '',
    modality: activity.modalityName ?? '',
    startsAt: activity.activityStartsAt ?? null,
    endsAt: activity.activityEndsAt ?? null,
    highDemandRoleIds: (activity.roleCapacities ?? [])
      .filter((capacity) => capacity.isHighDemand && capacity.activityRoleTypeId)
      .map((capacity) => capacity.activityRoleTypeId as string),
  }
}

/** Maps an activity for the admin edit form, dropping role capacities without a role id. */
export function toActivityDetail(activity: ActivityResponse): ActivityDetail {
  return {
    id: activity.id ?? '',
    title: activity.title ?? '',
    description: activity.description ?? '',
    location: activity.location ?? '',
    modalityId: activity.modalityId ?? '',
    startsAt: activity.activityStartsAt ?? null,
    endsAt: activity.activityEndsAt ?? null,
    thumbnailId: activity.thumbnailId ?? '',
    roleCapacities: (activity.roleCapacities ?? [])
      .filter((capacity) => capacity.activityRoleTypeId)
      .map((capacity) => ({
        roleTypeId: capacity.activityRoleTypeId as string,
        desiredCount: capacity.desiredCount ?? null,
      })),
  }
}

/** Maps the roles a household member may sign up for, skipping roles without id. */
export function toHouseholdSignupRoles(item: HouseholdSignupRolesResponse): HouseholdSignupRoles {
  return {
    userId: item.userId ?? '',
    roles: (item.roles ?? [])
      .filter((role) => role.id)
      .map((role) => ({
        id: role.id as string,
        name: role.name ?? i18n.global.t('entities.activity.fallback.role'),
      })),
  }
}

/** Maps one of the signed-in user's own assignments; a missing status is shown as `—`. */
export function toActivityAssignment(assignment: AssignedActivityResponse): ActivityAssignment {
  return {
    activityId: assignment.activityId ?? '',
    status: assignment.status?.name || '—',
    roleName: assignment.roleType?.name ?? '',
  }
}

/** Maps a household member's assignment, joining first and last name into `name`. */
export function toHouseholdActivityAssignment(
  assignment: HouseholdMemberAssignmentResponse,
): HouseholdActivityAssignment {
  return {
    activityId: assignment.activityId ?? '',
    userId: assignment.userId ?? '',
    name: `${assignment.firstName ?? ''} ${assignment.lastName ?? ''}`.trim(),
    roleName: assignment.roleName ?? '',
    status: assignment.statusName ?? '',
  }
}

/** Reduces a minor's `UserResponse` to the id and full name used by signup pickers. */
export function toHouseholdMember(child: UserResponse): HouseholdMember {
  return {
    id: child.id ?? '',
    name: `${child.firstName ?? ''} ${child.lastName ?? ''}`.trim(),
  }
}

/** Maps the schedule-conflict check run before signing a user up for an activity. */
export function toOverlapCheck(overlap: TimeOverlapResponse): OverlapCheck {
  const overlaps: ActivityOverlap[] = (overlap.overlaps ?? []).map((item) => ({
    activityId: item.activityId ?? '',
    title: item.title ?? '',
    startsAt: item.startsAt ?? null,
    endsAt: item.endsAt ?? null,
  }))

  return {
    hasOverlaps: overlap.hasOverlaps ?? false,
    overlaps,
  }
}
