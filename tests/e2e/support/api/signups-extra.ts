import type { ActivityResponse } from '@contract/activityResponse'
import type { AssignedActivityResponse } from '@contract/assignedActivityResponse'
import type { CreateActivityRequest } from '@contract/createActivityRequest'
import type { EventResponse } from '@contract/eventResponse'
import type { EventTermsStateResponse } from '@contract/eventTermsStateResponse'
import type { HouseholdMemberAssignmentResponse } from '@contract/householdMemberAssignmentResponse'
import type { UserResponse } from '@contract/userResponse'

import { birthDateForAge, hoursFromNow } from '../data/time'
import { uniqueTitle } from '../data/unique'
import type { Actor } from '../fixtures/actors'
import type { Account, Dependent } from './accounts'
import type { ApiClient } from './api-client'
import { CATALOG, type RoleName } from './catalogs'
import { SIGNATURE_PNG, type ContentSeeder, type EventOptions } from './content'

const FIRST_FREE_DAY = 30

let nextFreeDay = FIRST_FREE_DAY

/**
 * Creates an event on a day no earlier event of this worker uses. The worker actors sign up for
 * the activities of many tests, and the API warns about any two signups of a person that overlap
 * in time, so every test gets a day of its own.
 */
export function eventOnFreeDay(
  seed: ContentSeeder,
  options: EventOptions = {},
): Promise<EventResponse> {
  return seed.event({ startsIn: nextFreeDay++, ...options })
}

/** Creates an event that began yesterday and ends tomorrow, with its signup open. */
export function ongoingEvent(seed: ContentSeeder): Promise<EventResponse> {
  return seed.event({ title: uniqueTitle('Evento en curso'), startsIn: -1, lastsDays: 3 })
}

/** Creates an activity of `event` that started two hours ago and ends in an hour. */
export async function startedActivity(
  admin: ApiClient,
  seed: ContentSeeder,
  event: EventResponse,
): Promise<ActivityResponse> {
  const request: CreateActivityRequest = {
    title: uniqueTitle('Taller en curso'),
    description: 'Taller que ya ha empezado.',
    location: 'León',
    activityModalityTypeId: CATALOG.modalities.onSite,
    activityStartsAt: hoursFromNow(-2).toISOString(),
    activityEndsAt: hoursFromNow(1).toISOString(),
    thumbnailId: await seed.thumbnailId(),
    roleCapacities: [{ activityRoleTypeId: CATALOG.roles.participant, desiredCount: 10 }],
  }
  return admin.post(`/api/activities/${event.id}`, request)
}

/** The signups of the signed-in user of `api` to the activities of an event. */
export function assignedActivities(
  api: ApiClient,
  eventId: string,
): Promise<AssignedActivityResponse[]> {
  return api.get('/api/me/assigned-activities', { eventId })
}

/** The signups of the signed-in user of `api` and of the minors in their care to an event. */
export function householdSignups(
  api: ApiClient,
  eventId: string,
): Promise<HouseholdMemberAssignmentResponse[]> {
  return api.get(`/api/activities/household-assignments/${eventId}`)
}

/** The terms documents of an event and the decisions taken for each person of the household. */
export function termsState(api: ApiClient, eventId: string): Promise<EventTermsStateResponse> {
  return api.get(`/api/events/${eventId}/terms`)
}

/** Whether each terms document of an event was accepted for `userId`, by document id. */
export function decisionsOf(
  state: EventTermsStateResponse,
  userId: string,
): Record<string, boolean> {
  const person = state.people.find((candidate) => candidate.userId === userId)
  return Object.fromEntries(
    (person?.decisions ?? []).map((decision) => [decision.termsDocumentId, decision.accepted]),
  )
}

/**
 * Signs `userId` up for `activity` through `api`, accepting the terms documents of the event
 * except those listed in `declined`; accepted documents that ask for a signature are signed.
 */
export async function signUpDeclining(
  api: ApiClient,
  activity: ActivityResponse,
  userId: string,
  role: RoleName,
  declined: readonly string[],
): Promise<void> {
  const terms = await termsState(api, activity.eventId)
  await api.patch(`/api/activities/${activity.id}/${userId}/assign`, {
    activityRoleTypeId: CATALOG.roles[role],
    termsDecisions: terms.documents.map((document) => {
      const accepted = !declined.includes(document.termsDocumentId)
      return {
        termsDocumentId: document.termsDocumentId,
        accepted,
        signature: accepted && document.requiresSignature ? SIGNATURE_PNG : null,
        version: document.version,
      }
    }),
  })
}

/**
 * Adds to the household of `guardian` a minor of `age` named `firstName` with the guardian's last
 * name, the way the account page does, and returns it.
 */
export async function addMinor(
  guardian: Actor,
  firstName: string,
  age: number,
): Promise<Dependent> {
  const minor = {
    firstName,
    lastName: guardian.account.lastName,
    birthDate: birthDateForAge(age, -30),
    gender: 'PreferNotToSay',
  } as const
  const child = await guardian.api.post<UserResponse>(
    `/api/users/${guardian.account.id}/children`,
    { ...minor, declaresGuardianship: true },
  )
  return { ...minor, id: child.id }
}

/** The `index`-th dependent of an account that registered with minors. */
export function dependentOf(account: Account, index = 0): Dependent {
  const dependent = account.dependents[index]
  if (!dependent) throw new Error(`${account.email} has no dependent number ${index + 1}`)
  return dependent
}

/** The age in whole years on `day` (`YYYY-MM-DD`) of someone born on `birthDate`. */
export function ageOn(birthDate: string, day: string): number {
  const [birthYear = 0, birthMonth = 1, birthDay = 1] = birthDate.split('-').map(Number)
  const [year = 0, month = 1, date = 1] = day.slice(0, 10).split('-').map(Number)
  const hadBirthday = month > birthMonth || (month === birthMonth && date >= birthDay)
  return year - birthYear - (hadBirthday ? 0 : 1)
}
