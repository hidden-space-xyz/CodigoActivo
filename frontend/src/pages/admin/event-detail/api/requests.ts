import {
  getApiEventsEventIdRatings,
  getApiEventsEventIdTermsDecisions,
} from '@/shared/api/generated/endpoints/events/events'
import {
  getApiReportsEventsEventIdAttendees,
  getApiReportsEventsEventIdSummary,
} from '@/shared/api/generated/endpoints/reports/reports'
import { toPage } from '@/shared/api'
import type { ServerTablePage } from '@/shared/lib/paging'

import type {
  AttendeeListParams,
  EventAttendee,
  EventRating,
  EventStats,
  EventTermsDecision,
  RatingListParams,
  TermsDecisionListParams,
} from '../model/types'
import { toEventAttendee, toEventRating, toEventStats, toEventTermsDecision } from './mapper'

/** Loads the activity, signup and rating counts of an event. */
export async function getEventStatsRequest(eventId: string): Promise<EventStats> {
  const { data } = await getApiReportsEventsEventIdSummary(eventId)
  return toEventStats(data)
}

/** Fetches one page of the attendees of an event. */
export async function getEventAttendeesPageRequest({
  eventId,
  ...params
}: AttendeeListParams): Promise<ServerTablePage<EventAttendee>> {
  return toPage(await getApiReportsEventsEventIdAttendees(eventId, params), toEventAttendee)
}

/** Fetches one page of the anonymous ratings of an event. */
export async function getEventRatingsPageRequest({
  eventId,
  ...params
}: RatingListParams): Promise<ServerTablePage<EventRating>> {
  return toPage(await getApiEventsEventIdRatings(eventId, params), toEventRating)
}

/** Fetches one page of the terms decisions taken about an event. */
export async function getEventTermsDecisionsPageRequest({
  eventId,
  ...params
}: TermsDecisionListParams): Promise<ServerTablePage<EventTermsDecision>> {
  return toPage(await getApiEventsEventIdTermsDecisions(eventId, params), (decision) =>
    toEventTermsDecision(eventId, decision),
  )
}
