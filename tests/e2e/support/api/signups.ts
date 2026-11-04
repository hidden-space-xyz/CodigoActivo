import type { ActivityResponse } from '@contract/activityResponse'
import type { EventTermsStateResponse } from '@contract/eventTermsStateResponse'
import type { SaveEventRatingRequest } from '@contract/saveEventRatingRequest'

import type { ApiClient } from './api-client'
import { CATALOG, type RoleName } from './catalogs'
import { acceptAll } from './content'

/**
 * Signs `userId` (the signed-in user of `api` or one of their dependents) up for `activity` as
 * the SPA does, accepting every terms document of the event. The signup starts as requested.
 */
export async function signUp(
  api: ApiClient,
  activity: ActivityResponse,
  userId: string,
  role: RoleName = 'participant',
): Promise<void> {
  const terms = await api.get<EventTermsStateResponse>(`/api/events/${activity.eventId}/terms`)
  await api.patch(`/api/activities/${activity.id}/${userId}/assign`, {
    activityRoleTypeId: CATALOG.roles[role],
    termsDecisions: acceptAll(terms),
  })
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
