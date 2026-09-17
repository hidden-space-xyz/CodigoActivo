import {
  deleteApiActivitiesActivityId,
  getApiActivities,
  getApiActivitiesActivityId,
  getApiActivitiesActivityIdOverlapsUserId,
  getApiActivitiesHouseholdAssignmentsEventId,
  getApiActivitiesSignupRoles,
  patchApiActivitiesActivityIdUserIdAssign,
  patchApiActivitiesActivityIdUserIdChangeRole,
  patchApiActivitiesActivityIdUserIdChangeStatus,
  patchApiActivitiesActivityIdUserIdUnassign,
  postApiActivitiesActivityIdAssignHousehold,
  postApiActivitiesEventId,
  putApiActivitiesActivityId,
} from '@/shared/api/generated/endpoints/activities/activities'
import { getApiMeAssignedActivities } from '@/shared/api/generated/endpoints/me/me'
import { getApiUsers } from '@/shared/api/generated/endpoints/users/users'
import type {
  ActivityResponse,
  ChangeAssignmentRoleRequest,
  ChangeAssignmentStatusRequest,
  CreateActivityRequest,
  GetApiActivitiesParams,
  UpdateActivityRequest,
} from '@/shared/api/generated/models'
import { toPage, unwrapOrNull } from '@/shared/api'

import type { HouseholdAssignmentInput } from '../model/household-assignment-input'
import type {
  ActivityAssignment,
  ActivityDetail,
  EventActivity,
  HouseholdActivityAssignment,
  HouseholdMember,
  HouseholdSignupRoles,
  OverlapCheck,
  TermsDecisionInput,
} from '../model/types'
import {
  toActivityAssignment,
  toActivityDetail,
  toEventActivity,
  toHouseholdActivityAssignment,
  toHouseholdMember,
  toHouseholdSignupRoles,
  toOverlapCheck,
} from './mapper'

/** Loads an event's activities for the public timeline, ordered by start time (first 100). */
export async function getEventActivitiesRequest(
  eventId: string,
): Promise<readonly EventActivity[]> {
  const items = await listEventActivitiesRequest(eventId)
  return items.map(toEventActivity)
}

/** Loads the signed-in user's own assignments, limited to `eventId` when given. */
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

/** Loads an activity for the admin edit form; resolves `null` when it no longer exists (404). */
export async function getActivityByIdRequest(activityId: string): Promise<ActivityDetail | null> {
  const response = await unwrapOrNull<ActivityResponse>(getApiActivitiesActivityId(activityId))
  return response ? toActivityDetail(response) : null
}

/** Loads the assignments of the signed-in user's household (self and minors) for one event. */
export async function getHouseholdAssignmentsRequest(
  eventId: string,
): Promise<readonly HouseholdActivityAssignment[]> {
  const { data } = await getApiActivitiesHouseholdAssignmentsEventId(eventId)
  return (data ?? []).map(toHouseholdActivityAssignment)
}

/** Lists the minors under `userId` (first 100 by first name); the caller adds the user itself. */
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

/** Loads, per household member, the roles their user type allows them to sign up for. */
export async function getSignupRolesRequest(): Promise<readonly HouseholdSignupRoles[]> {
  const { data } = await getApiActivitiesSignupRoles()
  return (data ?? []).map(toHouseholdSignupRoles)
}

/** Checks whether `userId` already has other activities that overlap this one in time. */
export async function verifyOverlapsRequest(
  activityId: string,
  userId: string,
): Promise<OverlapCheck> {
  const { data } = await getApiActivitiesActivityIdOverlapsUserId(activityId, userId)
  return toOverlapCheck(data)
}

/**
 * Signs a user up for an activity with the given role. Pass `termsDecisions` with one entry per
 * pending terms document the user just decided on; otherwise the API rejects the signup with
 * `EventTermsAcceptanceRequired` when a required document is still undecided.
 */
export async function assignActivityRequest(
  activityId: string,
  userId: string,
  roleId: string,
  termsDecisions?: readonly TermsDecisionInput[],
): Promise<void> {
  await patchApiActivitiesActivityIdUserIdAssign(activityId, userId, {
    activityRoleTypeId: roleId,
    ...(termsDecisions
      ? {
          termsDecisions: termsDecisions.map((decision) => ({
            termsDocumentId: decision.termsDocumentId,
            accepted: decision.accepted,
          })),
        }
      : {}),
  })
}

/**
 * Signs several household members up for an activity in one request; `termsDecisions` works as in
 * `assignActivityRequest`.
 */
export async function assignHouseholdRequest(
  activityId: string,
  assignments: readonly HouseholdAssignmentInput[],
  termsDecisions?: readonly TermsDecisionInput[],
): Promise<void> {
  await postApiActivitiesActivityIdAssignHousehold(activityId, {
    assignments: assignments.map((assignment) => ({
      userId: assignment.userId,
      activityRoleTypeId: assignment.roleId,
    })),
    ...(termsDecisions
      ? {
          termsDecisions: termsDecisions.map((decision) => ({
            termsDocumentId: decision.termsDocumentId,
            accepted: decision.accepted,
          })),
        }
      : {}),
  })
}

/** Removes a user's signup from an activity. */
export async function unassignActivityRequest(activityId: string, userId: string): Promise<void> {
  await patchApiActivitiesActivityIdUserIdUnassign(activityId, userId)
}

/** Fetches one page of raw activities for the admin table with server-side filters and sorting. */
export function getActivitiesAdminPageRequest(
  params: GetApiActivitiesParams,
): Promise<{ items: ActivityResponse[]; total: number }> {
  return getApiActivities(params).then(toPage)
}

/** Lists an event's raw activities (first 100 by start time) for admin selectors. */
export function getEventActivityOptionsRequest(eventId: string): Promise<ActivityResponse[]> {
  return listEventActivitiesRequest(eventId)
}

/** Creates an activity inside `eventId` and resolves the created `ActivityResponse`. */
export function createActivityRequest(eventId: string, body: CreateActivityRequest) {
  return postApiActivitiesEventId(eventId, body).then((r) => r.data)
}

/** Replaces an activity's editable data and resolves the updated `ActivityResponse`. */
export function updateActivityRequest(id: string, body: UpdateActivityRequest) {
  return putApiActivitiesActivityId(id, body).then((r) => r.data)
}

/** Deletes an activity from the admin panel. */
export function deleteActivityRequest(id: string) {
  return deleteApiActivitiesActivityId(id)
}

/** Admin action that moves a user's assignment to another status (e.g. confirmed or denied). */
export function changeAssignmentStatusRequest(
  activityId: string,
  userId: string,
  body: ChangeAssignmentStatusRequest,
) {
  return patchApiActivitiesActivityIdUserIdChangeStatus(activityId, userId, body).then(
    (r) => r.data,
  )
}

/** Admin action that moves an existing assignment to a different activity role. */
export function changeAssignmentRoleRequest(
  activityId: string,
  userId: string,
  body: ChangeAssignmentRoleRequest,
) {
  return patchApiActivitiesActivityIdUserIdChangeRole(activityId, userId, body).then((r) => r.data)
}
