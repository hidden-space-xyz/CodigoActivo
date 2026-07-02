import {
  getApiActivitiesActivityIdUserIdVerifyTimeOverlaps,
  getApiActivitiesEventIdHouseholdAssignments,
  patchApiActivitiesActivityIdUserIdAssign,
  patchApiActivitiesActivityIdUserIdUnassign,
  postApiActivitiesActivityIdAssignHousehold,
} from '@/shared/api/generated/endpoints/activities/activities'
import { getApiUsersUserIdChildren } from '@/shared/api/generated/endpoints/users/users'
import type { ActivityResponse } from '@/shared/api/generated/models'
import type { AssignedActivityResponse } from '@/shared/api'
import { fetchODataEntity, fetchODataList, odataGuid } from '@/shared/api'

import type { HouseholdAssignmentInput } from '../model/household-assignment-input'
import type {
  ActivityAssignment,
  EventActivity,
  HouseholdActivityAssignment,
  HouseholdMember,
  OverlapCheck,
} from '../model/types'
import {
  toActivityAssignment,
  toEventActivity,
  toHouseholdActivityAssignment,
  toHouseholdMember,
  toOverlapCheck,
} from './mapper'

const ACTIVITIES = 'Activities'
const ASSIGNED_ACTIVITIES = 'AssignedActivities'

export async function getEventActivitiesRequest(eventId: string): Promise<readonly EventActivity[]> {
  const { items } = await fetchODataList<ActivityResponse>(ACTIVITIES, {
    filter: `eventId eq ${odataGuid(eventId)}`,
    orderBy: 'activityStartsAt asc',
    top: 1000,
  })
  return items.map(toEventActivity)
}

export async function getMyAssignmentsRequest(): Promise<readonly ActivityAssignment[]> {
  // The backend scopes AssignedActivities to the authenticated caller automatically.
  const { items } = await fetchODataList<AssignedActivityResponse>(ASSIGNED_ACTIVITIES, {
    orderBy: 'activityStartsAt asc',
    top: 1000,
  })
  return items.map(toActivityAssignment)
}

// Raw activity list of an event, ordered chronologically (admin table + assign dialog).
export async function listEventActivitiesRequest(eventId: string): Promise<ActivityResponse[]> {
  const { items } = await fetchODataList<ActivityResponse>(ACTIVITIES, {
    filter: `eventId eq ${odataGuid(eventId)}`,
    orderBy: 'activityStartsAt asc',
    top: 1000,
  })
  return items
}

// Single activity by id (admin edit dialog); null on 404.
export async function getActivityByIdRequest(
  activityId: string,
): Promise<ActivityResponse | null> {
  return fetchODataEntity<ActivityResponse>(ACTIVITIES, activityId)
}

export async function getHouseholdAssignmentsRequest(
  eventId: string,
): Promise<readonly HouseholdActivityAssignment[]> {
  const response = await getApiActivitiesEventIdHouseholdAssignments(eventId)
  return (response.data ?? []).map(toHouseholdActivityAssignment)
}

export async function getHouseholdMembersRequest(userId: string): Promise<readonly HouseholdMember[]> {
  const response = await getApiUsersUserIdChildren(userId)
  return (response.data ?? []).map(toHouseholdMember)
}

export async function verifyOverlapsRequest(
  activityId: string,
  userId: string,
): Promise<OverlapCheck> {
  const response = await getApiActivitiesActivityIdUserIdVerifyTimeOverlaps(activityId, userId)
  return toOverlapCheck(response.data)
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
