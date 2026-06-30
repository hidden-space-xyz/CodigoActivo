import {
  getApiActivitiesActivityIdUserIdVerifyTimeOverlaps,
  getApiActivitiesAssigned,
  getApiActivitiesEventId,
  getApiActivitiesEventIdHouseholdAssignments,
  patchApiActivitiesActivityIdUserIdAssign,
  patchApiActivitiesActivityIdUserIdUnassign,
  postApiActivitiesActivityIdAssignHousehold,
} from '@/shared/api/generated/endpoints/activities/activities'
import { getApiUsersUserIdChildren } from '@/shared/api/generated/endpoints/users/users'

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

export async function getEventActivitiesRequest(eventId: string): Promise<readonly EventActivity[]> {
  const response = await getApiActivitiesEventId(eventId)
  return (response.data ?? []).map(toEventActivity)
}

export async function getMyAssignmentsRequest(): Promise<readonly ActivityAssignment[]> {
  const response = await getApiActivitiesAssigned()
  return (response.data ?? []).map(toActivityAssignment)
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
