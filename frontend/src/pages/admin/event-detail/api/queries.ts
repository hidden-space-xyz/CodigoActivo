import { queryOptions } from '@tanstack/vue-query'

import type { ServerTableSource } from '@/shared/lib/paging'

import type {
  AttendeeListParams,
  EventAttendee,
  EventRating,
  RatingListParams,
} from '../model/types'
import {
  getEventAttendeesPageRequest,
  getEventRatingsPageRequest,
  getEventStatsRequest,
} from './requests'

/**
 * Query keys of the event reports, under their own root so activity and signup changes refresh
 * them without touching the event lists.
 */
export const eventReportKeys = {
  all: ['reports'] as const,
  stats: (eventId: string) => [...eventReportKeys.all, 'event-stats', eventId] as const,
  attendees: () => [...eventReportKeys.all, 'event-attendees'] as const,
  ratings: () => [...eventReportKeys.all, 'event-ratings'] as const,
}

/** Query options of the event reports. */
export const eventReportQueries = {
  /** Activity, signup and rating counts of an event. */
  stats: (eventId: string) =>
    queryOptions({
      queryKey: eventReportKeys.stats(eventId),
      queryFn: () => getEventStatsRequest(eventId),
    }),
}

/** Attendees of an event with their signups, paged, filtered and sorted by the API. */
export const attendeeList: ServerTableSource<EventAttendee, AttendeeListParams> = {
  queryKey: eventReportKeys.attendees(),
  fetchPage: (params) => getEventAttendeesPageRequest(params),
}

/** Anonymous ratings of an event, paged and sorted by the API. */
export const ratingList: ServerTableSource<EventRating, RatingListParams> = {
  queryKey: eventReportKeys.ratings(),
  fetchPage: (params) => getEventRatingsPageRequest(params),
}
