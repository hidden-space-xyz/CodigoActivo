import type {
  ActivityResponse,
  AssignedActivityResponse,
  HouseholdMemberAssignmentResponse,
  TimeOverlapResponse,
  UserResponse,
} from '@/shared/api/generated/models'

import type {
  ActivityAssignment,
  ActivityOverlap,
  ActivityRole,
  EventActivity,
  HouseholdActivityAssignment,
  HouseholdMember,
  OverlapCheck,
} from '../model/types'

export function toEventActivity(activity: ActivityResponse): EventActivity {
  const roles: ActivityRole[] = (activity.allowedRoleTypes ?? [])
    .filter((role) => role.roleTypeId)
    .map((role) => ({ id: role.roleTypeId as string, name: role.roleTypeName ?? 'Rol' }))

  return {
    id: activity.id ?? '',
    title: activity.title ?? 'Actividad',
    description: activity.description ?? '',
    startsAt: activity.activityStartsAt ?? null,
    endsAt: activity.activityEndsAt ?? null,
    roles,
  }
}

export function toActivityAssignment(assignment: AssignedActivityResponse): ActivityAssignment {
  return {
    activityId: assignment.activityId ?? '',
    status: assignment.status?.name ?? '—',
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
