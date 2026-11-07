import type { ActivityResponse } from '@contract/activityResponse'
import type { AssignedActivityResponse } from '@contract/assignedActivityResponse'
import type { EventResponse } from '@contract/eventResponse'
import type { EventTermsStateResponse } from '@contract/eventTermsStateResponse'
import type { HouseholdMemberAssignmentResponse } from '@contract/householdMemberAssignmentResponse'
import type { SaveEventRatingRequest } from '@contract/saveEventRatingRequest'

import { uniqueTitle } from '../data/unique'
import type { ApiClient } from './api-client'
import { CATALOG, type RoleName } from './catalogs'
import type { ContentSeeder, EventOptions } from './content'

const FIRST_FREE_DAY = 30

/** A handwritten signature as the signature pad sends it: a base64 PNG. */
const SIGNATURE_PNG =
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg=='

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
 * Signs `userId` (the signed-in user of `api` or one of their dependents) up for `activity` as the
 * SPA does, accepting the terms documents of the event except those whose ids are in `declined`
 * and signing the accepted ones that ask for it. The signup starts as requested.
 */
export async function signUp(
  api: ApiClient,
  activity: ActivityResponse,
  userId: string,
  role: RoleName = 'participant',
  declined: readonly string[] = [],
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

/** Rates an event that has ended, as the signed-in user of `api`. */
export async function rateEvent(
  api: ApiClient,
  eventId: string,
  rating: SaveEventRatingRequest,
): Promise<void> {
  await api.put(`/api/events/${eventId}/rating`, {
    score: rating.score ?? null,
    mostLiked: rating.mostLiked ?? null,
    leastLiked: rating.leastLiked ?? null,
    suggestions: rating.suggestions ?? null,
  })
}
