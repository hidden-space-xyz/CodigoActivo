import { queryOptions } from '@tanstack/vue-query'

import type { PagedListSource, ServerTableSource } from '@/shared/lib/paging'

import type { EventListing, EventListParams, EventSummary, PastEventFilters } from '../model/types'
import {
  getEventLeaderRosterRequest,
  getEventRequest,
  getEventsPageRequest,
  getEventTermsStateRequest,
  getHomeEventsRequest,
  getPastEventCategoriesRequest,
  getPastEventsPageRequest,
  getPastEventYearsRequest,
  getUpcomingEventsPageRequest,
} from './requests'

/** Query keys of events; `all` covers every one of them. */
export const eventKeys = {
  all: ['events'] as const,
  upcoming: () => [...eventKeys.all, 'upcoming'] as const,
  past: (filters: PastEventFilters) =>
    [...eventKeys.all, 'past', filters.year, filters.search, filters.categoryId] as const,
  pastYears: () => [...eventKeys.all, 'past-years'] as const,
  pastCategories: () => [...eventKeys.all, 'past-categories'] as const,
  home: () => [...eventKeys.all, 'board'] as const,
  detail: (id: string) => [...eventKeys.all, 'detail', id] as const,
  terms: (id: string) => [...eventKeys.all, 'terms', id] as const,
  leaderRoster: (id: string, userId: string) =>
    [...eventKeys.all, 'leader-roster', id, userId] as const,
  list: () => [...eventKeys.all, 'list'] as const,
}

/** Query options of events. */
export const eventQueries = {
  /** A whole event, or `null` when it does not exist. */
  detail: (id: string) =>
    queryOptions({
      queryKey: eventKeys.detail(id),
      queryFn: () => getEventRequest(id),
    }),
  /** Home page board: the featured event plus up to three other upcoming events. */
  home: () =>
    queryOptions({
      queryKey: eventKeys.home(),
      queryFn: () => getHomeEventsRequest(),
    }),
  /** Years that have past events. */
  pastYears: () =>
    queryOptions({
      queryKey: eventKeys.pastYears(),
      queryFn: () => getPastEventYearsRequest(),
    }),
  /** Categories used by past events. */
  pastCategories: () =>
    queryOptions({
      queryKey: eventKeys.pastCategories(),
      queryFn: () => getPastEventCategoriesRequest(),
    }),
  /** The signed-in user's decision state for the event's terms documents. */
  terms: (eventId: string) =>
    queryOptions({
      queryKey: eventKeys.terms(eventId),
      queryFn: () => getEventTermsStateRequest(eventId),
    }),
  /**
   * Activities of an event that `userId` leads, with their confirmed attendees; the key includes
   * the user so one account's attendee data is never served to another session.
   */
  leaderRoster: (eventId: string, userId: string) =>
    queryOptions({
      queryKey: eventKeys.leaderRoster(eventId, userId),
      queryFn: () => getEventLeaderRosterRequest(eventId),
    }),
}

/** Public event lists, loaded page by page. */
export const eventPages = {
  /** Upcoming events ordered by start date. */
  upcoming: (): PagedListSource<EventSummary> => ({
    queryKey: eventKeys.upcoming(),
    fetchPage: (page, pageSize) => getUpcomingEventsPageRequest(page, pageSize),
  }),
  /** Past events of the chosen year, newest first, filtered by search and category. */
  past: (filters: PastEventFilters): PagedListSource<EventSummary> => ({
    queryKey: eventKeys.past(filters),
    fetchPage: (page, pageSize) => getPastEventsPageRequest(filters, page, pageSize),
  }),
}

/** Admin events table, paged, filtered and sorted by the API. */
export const eventList: ServerTableSource<EventListing, EventListParams> = {
  queryKey: eventKeys.list(),
  fetchPage: (params) => getEventsPageRequest(params),
}
