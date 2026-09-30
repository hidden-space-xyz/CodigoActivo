import {
  deleteApiActivitiesActivityId,
  getApiActivities,
  getApiActivitiesActivityId,
  getApiActivitiesActivityIdOverlapsUserId,
  getApiActivitiesAssignmentStatusTypes,
  getApiActivitiesHouseholdAssignmentsEventId,
  getApiActivitiesModalityTypes,
  getApiActivitiesRoleType,
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
import { toPage, unwrapOrNull } from '@/shared/api'
import type { ServerTablePage } from '@/shared/lib/paging'

import type { HouseholdAssignmentInput } from '../model/household-assignment-input'
import type {
  ActivityAssignment,
  ActivityDetail,
  ActivityInput,
  ActivityListing,
  ActivityListParams,
  ActivityModality,
  ActivityRole,
  AssignmentStatus,
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
  toActivityListing,
  toActivityModality,
  toActivityRequest,
  toActivityRole,
  toAssignmentStatus,
  toEventActivity,
  toHouseholdActivityAssignment,
  toHouseholdMember,
  toHouseholdSignupRoles,
  toOverlapCheck,
} from './mapper'

/** Loads an event's activities for its timeline, ordered by start time (first 100). */
export async function getEventActivitiesRequest(
  eventId: string,
): Promise<readonly EventActivity[]> {
  const { items } = toPage(
    await getApiActivities({ eventId, pageSize: 100, sort: 'activityStartsAt' }),
  )
  return items.map(toEventActivity)
}

/** Loads the signed-in user's own signups to the activities of an event. */
export async function getMyAssignmentsRequest(
  eventId: string,
): Promise<readonly ActivityAssignment[]> {
  const { data } = await getApiMeAssignedActivities({ eventId })
  return data.map(toActivityAssignment)
}

/** Loads the signups of the signed-in user's household (self and minors) for one event. */
export async function getHouseholdAssignmentsRequest(
  eventId: string,
): Promise<readonly HouseholdActivityAssignment[]> {
  const { data } = await getApiActivitiesHouseholdAssignmentsEventId(eventId)
  return data.map(toHouseholdActivityAssignment)
}

/** Lists the minors under `userId` (first 100 by first name); the caller adds the user itself. */
export async function getHouseholdMembersRequest(
  userId: string,
): Promise<readonly HouseholdMember[]> {
  const { items } = toPage(
    await getApiUsers({ parentId: userId, pageSize: 100, sort: 'firstName' }),
  )
  return items.map(toHouseholdMember)
}

/** Loads, per household member, the roles their user type allows them to sign up for. */
export async function getSignupRolesRequest(): Promise<readonly HouseholdSignupRoles[]> {
  const { data } = await getApiActivitiesSignupRoles()
  return data.map(toHouseholdSignupRoles)
}

/** Checks whether `userId` already has other activities that overlap this one in time. */
export async function verifyOverlapsRequest(
  activityId: string,
  userId: string,
): Promise<OverlapCheck> {
  const { data } = await getApiActivitiesActivityIdOverlapsUserId(activityId, userId)
  return toOverlapCheck(data)
}

function toTermsDecisions(termsDecisions?: readonly TermsDecisionInput[]) {
  return termsDecisions
    ? {
        termsDecisions: termsDecisions.map((decision) => ({
          termsDocumentId: decision.termsDocumentId,
          accepted: decision.accepted,
        })),
      }
    : {}
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
    ...toTermsDecisions(termsDecisions),
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
    ...toTermsDecisions(termsDecisions),
  })
}

/** Removes a user's signup from an activity. */
export async function unassignActivityRequest(activityId: string, userId: string): Promise<void> {
  await patchApiActivitiesActivityIdUserIdUnassign(activityId, userId)
}

/** Fetches one page of the admin activities table. */
export async function getActivitiesPageRequest(
  params: ActivityListParams,
): Promise<ServerTablePage<ActivityListing>> {
  const { items, total } = toPage(await getApiActivities(params))
  return { items: items.map(toActivityListing), total }
}

/** Loads an activity for the admin edit form; resolves `null` when it no longer exists (404). */
export async function getActivityRequest(activityId: string): Promise<ActivityDetail | null> {
  const activity = await unwrapOrNull(getApiActivitiesActivityId(activityId))
  return activity ? toActivityDetail(activity) : null
}

/** Creates an activity inside an event. */
export async function createActivityRequest(eventId: string, input: ActivityInput): Promise<void> {
  await postApiActivitiesEventId(eventId, toActivityRequest(input))
}

/** Replaces an activity's editable data. */
export async function updateActivityRequest(id: string, input: ActivityInput): Promise<void> {
  await putApiActivitiesActivityId(id, toActivityRequest(input))
}

/** Deletes an activity with its signups. */
export async function deleteActivityRequest(id: string): Promise<void> {
  await deleteApiActivitiesActivityId(id)
}

/** Moves a user's signup to another status, such as confirmed or denied. */
export async function changeAssignmentStatusRequest(
  activityId: string,
  userId: string,
  statusId: string,
): Promise<void> {
  await patchApiActivitiesActivityIdUserIdChangeStatus(activityId, userId, {
    assignmentStatusId: statusId,
  })
}

/** Moves a user's signup to another role of the same activity. */
export async function changeAssignmentRoleRequest(
  activityId: string,
  userId: string,
  roleTypeId: string,
): Promise<void> {
  await patchApiActivitiesActivityIdUserIdChangeRole(activityId, userId, {
    activityRoleTypeId: roleTypeId,
  })
}

/** Lists the roles a participant can take in an activity. */
export async function getActivityRolesRequest(): Promise<readonly ActivityRole[]> {
  const { data } = await getApiActivitiesRoleType()
  return data.map(toActivityRole)
}

/** Lists the statuses an activity signup can move through. */
export async function getAssignmentStatusesRequest(): Promise<readonly AssignmentStatus[]> {
  const { data } = await getApiActivitiesAssignmentStatusTypes()
  return data.map(toAssignmentStatus)
}

/** Lists the modalities an activity can be offered in. */
export async function getActivityModalitiesRequest(): Promise<readonly ActivityModality[]> {
  const { data } = await getApiActivitiesModalityTypes()
  return data.map(toActivityModality)
}
