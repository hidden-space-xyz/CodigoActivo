import type { ActivityResponse, UserResponse } from '@/shared/api/generated/models'
import type {
  AssignedActivityResponse,
  HouseholdMemberAssignmentResponse,
  HouseholdSignupRolesResponse,
  TimeOverlapResponse,
} from '@/shared/api/generated/models'

import type {
  ActivityAssignment,
  ActivityOverlap,
  EventActivity,
  HouseholdActivityAssignment,
  HouseholdMember,
  HouseholdSignupRoles,
  OverlapCheck,
} from '../model/types'

export function toEventActivity(activity: ActivityResponse): EventActivity {
  return {
    id: activity.id ?? '',
    title: activity.title ?? 'Actividad',
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

export function toHouseholdSignupRoles(item: HouseholdSignupRolesResponse): HouseholdSignupRoles {
  return {
    userId: item.userId ?? '',
    roles: (item.roles ?? [])
      .filter((role) => role.id)
      .map((role) => ({ id: role.id as string, name: role.name ?? 'Rol' })),
  }
}

export function toActivityAssignment(assignment: AssignedActivityResponse): ActivityAssignment {
  return {
    activityId: assignment.activityId ?? '',
    status: assignment.status?.name || '—',
    roleName: assignment.roleType?.name ?? '',
  }
}

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

export function toHouseholdMember(child: UserResponse): HouseholdMember {
  return {
    id: child.id ?? '',
    name: `${child.firstName ?? ''} ${child.lastName ?? ''}`.trim(),
  }
}

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
