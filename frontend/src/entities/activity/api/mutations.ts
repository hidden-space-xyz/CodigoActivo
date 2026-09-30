import { mutationOptions } from '@tanstack/vue-query'

import type { HouseholdAssignmentInput } from '../model/household-assignment-input'
import type { ActivityInput, TermsDecisionInput } from '../model/types'
import { activityKeys } from './queries'
import {
  assignActivityRequest,
  assignHouseholdRequest,
  changeAssignmentRoleRequest,
  changeAssignmentStatusRequest,
  createActivityRequest,
  deleteActivityRequest,
  unassignActivityRequest,
  updateActivityRequest,
} from './requests'

function signupKeys(eventId: string) {
  return [
    activityKeys.ofEvent(eventId),
    activityKeys.myAssignments(eventId),
    activityKeys.householdAssignments(eventId),
  ]
}

/** A signup to one activity, with the decisions on terms documents the user just made. */
interface SignupVariables {
  readonly activityId: string
  readonly termsDecisions?: readonly TermsDecisionInput[] | undefined
}

/** A signup an admin changes: the activity and the user signed up to it. */
interface AssignmentVariables {
  readonly activityId: string
  readonly userId: string
}

/**
 * Mutation options of activities and their signups. Signups refresh the event's activities and
 * the signups of the user and their household; admin changes refresh every activity query.
 */
export const activityMutations = {
  /** Signs `userId` up with a role. */
  signUp: (eventId: string, userId: string) =>
    mutationOptions({
      mutationFn: ({ activityId, roleId, termsDecisions }: SignupVariables & { roleId: string }) =>
        assignActivityRequest(activityId, userId, roleId, termsDecisions),
      meta: { invalidates: signupKeys(eventId) },
    }),
  /** Signs several household members up, each with a role. */
  signUpHousehold: (eventId: string) =>
    mutationOptions({
      mutationFn: ({
        activityId,
        assignments,
        termsDecisions,
      }: SignupVariables & { assignments: readonly HouseholdAssignmentInput[] }) =>
        assignHouseholdRequest(activityId, assignments, termsDecisions),
      meta: { invalidates: signupKeys(eventId) },
    }),
  /** Withdraws the signup of the user or one of their minors. */
  withdraw: (eventId: string) =>
    mutationOptions({
      mutationFn: ({ activityId, userId }: AssignmentVariables) =>
        unassignActivityRequest(activityId, userId),
      meta: { invalidates: signupKeys(eventId) },
    }),
  /** Creates an activity inside `eventId`. */
  create: (eventId: string) =>
    mutationOptions({
      mutationFn: (input: ActivityInput) => createActivityRequest(eventId, input),
      meta: { invalidates: [activityKeys.all] },
    }),
  update: () =>
    mutationOptions({
      mutationFn: ({ id, input }: { id: string; input: ActivityInput }) =>
        updateActivityRequest(id, input),
      meta: { invalidates: [activityKeys.all] },
    }),
  remove: () =>
    mutationOptions({
      mutationFn: (id: string) => deleteActivityRequest(id),
      meta: { invalidates: [activityKeys.all] },
    }),
  /** Moves a signup to another status. */
  changeStatus: () =>
    mutationOptions({
      mutationFn: ({ activityId, userId, statusId }: AssignmentVariables & { statusId: string }) =>
        changeAssignmentStatusRequest(activityId, userId, statusId),
      meta: { invalidates: [activityKeys.all] },
    }),
  /** Moves a signup to another role. */
  changeRole: () =>
    mutationOptions({
      mutationFn: ({ activityId, userId, roleId }: AssignmentVariables & { roleId: string }) =>
        changeAssignmentRoleRequest(activityId, userId, roleId),
      meta: { invalidates: [activityKeys.all] },
    }),
}
