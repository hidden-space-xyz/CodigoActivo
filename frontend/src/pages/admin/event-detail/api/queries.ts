import { queryOptions } from '@tanstack/vue-query'

import type { ServerTableSource } from '@/shared/lib/paging'

import type {
  AttendeeListParams,
  EventAttendee,
  EventRating,
  EventTermsDecision,
  RatingListParams,
  TermsDecisionListParams,
} from '../model/types'
import {
  getEventAttendeesPageRequest,
  getEventRatingsPageRequest,
  getEventStatsRequest,
  getEventTermsDecisionsPageRequest,
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
  termsDecisions: () => [...eventReportKeys.all, 'event-terms-decisions'] as const,
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

/** Ratings of an event without their authors, paged and sorted by the API. */
export const ratingList: ServerTableSource<EventRating, RatingListParams> = {
  queryKey: eventReportKeys.ratings(),
  fetchPage: (params) => getEventRatingsPageRequest(params),
}

/** Terms decisions taken about an event, paged, filtered and sorted by the API. */
export const termsDecisionList: ServerTableSource<EventTermsDecision, TermsDecisionListParams> = {
  queryKey: eventReportKeys.termsDecisions(),
  fetchPage: (params) => getEventTermsDecisionsPageRequest(params),
}
