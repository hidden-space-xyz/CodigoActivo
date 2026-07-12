import {
  getApiActivities,
  getApiActivitiesActivityId,
  getApiActivitiesActivityIdOverlapsUserId,
  getApiActivitiesHouseholdAssignmentsEventId,
  getApiActivitiesSignupRoles,
  patchApiActivitiesActivityIdUserIdAssign,
  patchApiActivitiesActivityIdUserIdUnassign,
  postApiActivitiesActivityIdAssignHousehold,
} from '@/shared/api/generated/endpoints/activities/activities'
import { getApiMeAssignedActivities } from '@/shared/api/generated/endpoints/me/me'
import { getApiUsers } from '@/shared/api/generated/endpoints/users/users'
import type { ActivityResponse } from '@/shared/api/generated/models'
import { toPage, unwrapOrNull } from '@/shared/api'

import type { HouseholdAssignmentInput } from '../model/household-assignment-input'
import type {
  ActivityAssignment,
  EventActivity,
  HouseholdActivityAssignment,
  HouseholdMember,
  HouseholdSignupRoles,
  OverlapCheck,
} from '../model/types'
import {
  toActivityAssignment,
  toEventActivity,
  toHouseholdActivityAssignment,
  toHouseholdMember,
  toHouseholdSignupRoles,
  toOverlapCheck,
} from './mapper'

export async function getEventActivitiesRequest(
  eventId: string,
): Promise<readonly EventActivity[]> {
  const items = await listEventActivitiesRequest(eventId)
  return items.map(toEventActivity)
}

export async function getMyAssignmentsRequest(
  eventId?: string,
): Promise<readonly ActivityAssignment[]> {
  const { data } = await getApiMeAssignedActivities(eventId ? { eventId } : {})
  return (data ?? []).map(toActivityAssignment)
}

async function listEventActivitiesRequest(eventId: string): Promise<ActivityResponse[]> {
  const { items } = await getApiActivities({
    eventId,
    pageSize: 100,
    sort: 'activityStartsAt',
  }).then(toPage)
  return items
}

export async function getActivityByIdRequest(activityId: string): Promise<ActivityResponse | null> {
  return unwrapOrNull<ActivityResponse>(getApiActivitiesActivityId(activityId))
}

export async function getHouseholdAssignmentsRequest(
  eventId: string,
): Promise<readonly HouseholdActivityAssignment[]> {
  const { data } = await getApiActivitiesHouseholdAssignmentsEventId(eventId)
  return (data ?? []).map(toHouseholdActivityAssignment)
}

export async function getHouseholdMembersRequest(
  userId: string,
): Promise<readonly HouseholdMember[]> {
  const { items } = await getApiUsers({
    parentId: userId,
    pageSize: 100,
    sort: 'firstName',
  }).then(toPage)
  return items.map(toHouseholdMember)
}

export async function getSignupRolesRequest(): Promise<readonly HouseholdSignupRoles[]> {
  const { data } = await getApiActivitiesSignupRoles()
  return (data ?? []).map(toHouseholdSignupRoles)
}

export async function verifyOverlapsRequest(
  activityId: string,
  userId: string,
): Promise<OverlapCheck> {
  const { data } = await getApiActivitiesActivityIdOverlapsUserId(activityId, userId)
  return toOverlapCheck(data)
}

export async function assignActivityRequest(
  activityId: string,
  userId: string,
  roleId: string,
): Promise<void> {
  await patchApiActivitiesActivityIdUserIdAssign(activityId, userId, {
    activityRoleTypeId: roleId,
  })
}

export async function assignHouseholdRequest(
  activityId: string,
  assignments: readonly HouseholdAssignmentInput[],
): Promise<void> {
  await postApiActivitiesActivityIdAssignHousehold(activityId, {
    assignments: assignments.map((assignment) => ({
      userId: assignment.userId,
      activityRoleTypeId: assignment.roleId,
    })),
  })
}

export async function unassignActivityRequest(activityId: string, userId: string): Promise<void> {
  await patchApiActivitiesActivityIdUserIdUnassign(activityId, userId)
}
