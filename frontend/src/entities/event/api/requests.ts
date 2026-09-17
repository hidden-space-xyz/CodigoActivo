import {
  deleteApiEventsEventId,
  getApiEvents,
  getApiEventsEventId,
  getApiEventsEventIdRatings,
  getApiEventsEventIdSignupStats,
  getApiEventsEventIdTerms,
  getApiEventsPastCategories,
  getApiEventsPastYears,
  patchApiEventsEventIdFeature,
  postApiEvents,
  putApiEventsEventId,
} from '@/shared/api/generated/endpoints/events/events'
import {
  getApiReportsDashboardAnalytics,
  getApiReportsEventsEventIdAttendees,
  getApiReportsEventsEventIdBadges,
  getApiReportsEventsEventIdRoster,
  getApiReportsEventsEventIdSummary,
} from '@/shared/api/generated/endpoints/reports/reports'
import type {
  CreateEventRequest,
  EventListItemResponse,
  EventResponse,
  GetApiEventsEventIdRatingsParams,
  GetApiEventsParams,
  GetApiReportsDashboardAnalyticsParams,
  GetApiReportsEventsEventIdAttendeesParams,
  UpdateEventRequest,
} from '@/shared/api/generated/models'
import { FEATURED_FIRST_SORT, toPage, unwrapOrNull } from '@/shared/api'
import type { PagedListPage } from '@/shared/lib'

import type {
  EventCategoryTag,
  EventDetail,
  EventSignupStats,
  EventTermsState,
  HomeEvents,
  PastEvent,
  PastEventFilters,
  UpcomingEvent,
} from '../model/types'
import {
  toCategoryTag,
  toEventDetail,
  toEventSignupStats,
  toEventTermsState,
  toPastEvent,
  toUpcomingEvent,
} from './mapper'

/** Fetches one page of upcoming events ordered by start date, mapped to card models. */
export async function getUpcomingEventsPageRequest(
  page: number,
  pageSize: number,
): Promise<PagedListPage<UpcomingEvent>> {
  const result = await getApiEvents({ scope: 'Upcoming', sort: 'eventStartsAt', page, pageSize })
  const { items, total } = toPage(result)
  return { items: items.map(toUpcomingEvent), total }
}

/** Lists the years that have past events, as strings for select options. */
export async function getPastEventYearsRequest(): Promise<readonly string[]> {
  const { data } = await getApiEventsPastYears()
  return (data ?? []).map(String)
}

/** Lists categories used by past events, dropping entries without an id. */
export async function getPastEventCategoriesRequest(): Promise<readonly EventCategoryTag[]> {
  const { data } = await getApiEventsPastCategories()
  return (data ?? []).map(toCategoryTag).filter((category) => category.id)
}

/** Fetches one page of past events for a year, newest first, with optional search and category. */
export async function getPastEventsPageRequest(
  filters: PastEventFilters,
  page: number,
  pageSize: number,
): Promise<PagedListPage<PastEvent>> {
  const result = await getApiEvents({
    scope: 'Past',
    year: Number(filters.year),
    ...(filters.search ? { search: filters.search } : {}),
    ...(filters.categoryId ? { categoryTypeId: filters.categoryId } : {}),
    sort: '-eventStartsAt',
    page,
    pageSize,
  })
  const { items, total } = toPage(result)
  return { items: items.map(toPastEvent), total }
}

async function getFeaturedEventRequest(): Promise<UpcomingEvent | null> {
  const { data } = await getApiEvents({ sort: FEATURED_FIRST_SORT, pageSize: 1 })
  const first = data.items?.[0]
  return first ? toUpcomingEvent(first) : null
}

/** Loads the public event detail; resolves to `null` when the event does not exist (404). */
export async function getEventByIdRequest(id: string): Promise<EventDetail | null> {
  const event = await unwrapOrNull<EventResponse>(getApiEventsEventId(id))
  return event ? toEventDetail(event) : null
}

/** Loads the signed-in user's decision state for every terms document linked to the event. */
export async function getEventTermsStateRequest(eventId: string): Promise<EventTermsState> {
  const { data } = await getApiEventsEventIdTerms(eventId)
  return toEventTermsState(data)
}

/** Loads the event's signup statistics (admin/member only; the backend enforces the check). */
export async function getEventSignupStatsRequest(eventId: string): Promise<EventSignupStats> {
  const { data } = await getApiEventsEventIdSignupStats(eventId)
  return toEventSignupStats(data)
}

/**
 * Builds the home board: the featured-first event plus up to three upcoming events, excluding the
 * featured one so it is not shown twice.
 */
export async function getHomeEventsRequest(): Promise<HomeEvents> {
  const [featured, upcomingPage] = await Promise.all([
    getFeaturedEventRequest(),
    getApiEvents({ scope: 'Upcoming', sort: 'eventStartsAt', pageSize: 4 }),
  ])
  const upcoming = (upcomingPage.data.items ?? []).map(toUpcomingEvent)
  const items = upcoming.filter((event) => event.id !== featured?.id).slice(0, 3)
  return { featured, items }
}

/** Fetches one page of the admin events table with raw list items (no card mapping). */
export function getEventsAdminPageRequest(
  params: GetApiEventsParams,
): Promise<{ items: EventListItemResponse[]; total: number }> {
  return getApiEvents(params).then(toPage)
}

/** Loads the raw event for the admin editor; `null` when it does not exist (404). */
export function getEventAdminRequest(id: string) {
  return unwrapOrNull<EventResponse>(getApiEventsEventId(id))
}

/** Creates an event and resolves to the created event. */
export function createEventRequest(body: CreateEventRequest) {
  return postApiEvents(body).then((r) => r.data)
}

/** Replaces an event and resolves to the updated event. */
export function updateEventRequest(id: string, body: UpdateEventRequest) {
  return putApiEventsEventId(id, body).then((r) => r.data)
}

/** Deletes an event (admin only); resolves with the raw 204 response. */
export function deleteEventRequest(id: string) {
  return deleteApiEventsEventId(id)
}

/**
 * Makes the event the only featured one (any other featured event loses the flag) and resolves to
 * the updated event. Despite the name, calling it on the featured event does not unfeature it.
 */
export function toggleEventFeatureRequest(id: string) {
  return patchApiEventsEventIdFeature(id).then((r) => r.data)
}

/** Fetches one page of attendee ratings for an event. */
export function getEventRatingsPageRequest(
  eventId: string,
  params: GetApiEventsEventIdRatingsParams,
) {
  return getApiEventsEventIdRatings(eventId, params).then(toPage)
}

/** Loads the admin report summary for an event. */
export function getEventSummaryRequest(eventId: string) {
  return getApiReportsEventsEventIdSummary(eventId).then((r) => r.data)
}

/** Fetches one page of the attendee report for an event. */
export function getEventAttendeesPageRequest(
  eventId: string,
  params: GetApiReportsEventsEventIdAttendeesParams,
) {
  return getApiReportsEventsEventIdAttendees(eventId, params).then(toPage)
}

/** Loads one badge per attendee (name, guardian, activities) for printing (admin only). */
export function getEventBadgesRequest(eventId: string) {
  return getApiReportsEventsEventIdBadges(eventId).then((r) => r.data)
}

/** Loads participants grouped by activity, with contact and guardian data (admin only). */
export function getEventRosterRequest(eventId: string) {
  return getApiReportsEventsEventIdRoster(eventId).then((r) => r.data)
}

/** Loads admin dashboard analytics for a date range. */
export function getDashboardAnalyticsRequest(params: GetApiReportsDashboardAnalyticsParams) {
  return getApiReportsDashboardAnalytics(params).then((r) => r.data)
}
