import { getApiEventsEventIdRatings } from '@/shared/api/generated/endpoints/events/events'
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
  RatingListParams,
} from '../model/types'
import { toEventAttendee, toEventRating, toEventStats } from './mapper'

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
  const { items, total } = toPage(await getApiReportsEventsEventIdAttendees(eventId, params))
  return { items: items.map(toEventAttendee), total }
}

/** Fetches one page of the anonymous ratings of an event. */
export async function getEventRatingsPageRequest({
  eventId,
  ...params
}: RatingListParams): Promise<ServerTablePage<EventRating>> {
  const { items, total } = toPage(await getApiEventsEventIdRatings(eventId, params))
  return { items: items.map(toEventRating), total }
}
