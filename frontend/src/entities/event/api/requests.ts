import {
  deleteApiEventsEventId,
  getApiEvents,
  getApiEventsEventId,
  getApiEventsEventIdLeaderRoster,
  getApiEventsEventIdTerms,
  getApiEventsPastCategories,
  getApiEventsPastYears,
  patchApiEventsEventIdFeature,
  postApiEvents,
  putApiEventsEventId,
} from '@/shared/api/generated/endpoints/events/events'
import { toPage, unwrapOrNull } from '@/shared/api'
import type { PagedListPage, ServerTablePage } from '@/shared/lib/paging'

import type {
  EventCategoryTag,
  EventDetail,
  EventInput,
  EventListing,
  EventListParams,
  EventSummary,
  EventTermsState,
  HomeEvents,
  LeaderRosterActivity,
  PastEventFilters,
} from '../model/types'
import {
  toCategoryTag,
  toEventDetail,
  toEventListing,
  toEventRequest,
  toEventSummary,
  toEventTermsState,
  toLeaderRosterActivity,
  toPastEventSummary,
} from './mapper'

/** Fetches one page of upcoming events ordered by start date. */
export async function getUpcomingEventsPageRequest(
  page: number,
  pageSize: number,
): Promise<PagedListPage<EventSummary>> {
  const response = await getApiEvents({ scope: 'Upcoming', sort: 'eventStartsAt', page, pageSize })
  const { items, total } = toPage(response)
  return { items: items.map(toEventSummary), total }
}

/** Lists the years that have past events, as strings for select options. */
export async function getPastEventYearsRequest(): Promise<readonly string[]> {
  const { data } = await getApiEventsPastYears()
  return data.map(String)
}

/** Lists the categories used by past events. */
export async function getPastEventCategoriesRequest(): Promise<readonly EventCategoryTag[]> {
  const { data } = await getApiEventsPastCategories()
  return data.map(toCategoryTag)
}

/** Fetches one page of past events for a year, newest first, with optional search and category. */
export async function getPastEventsPageRequest(
  filters: PastEventFilters,
  page: number,
  pageSize: number,
): Promise<PagedListPage<EventSummary>> {
  const response = await getApiEvents({
    scope: 'Past',
    year: Number(filters.year),
    ...(filters.search ? { search: filters.search } : {}),
    ...(filters.categoryId ? { categoryTypeId: filters.categoryId } : {}),
    sort: '-eventStartsAt',
    page,
    pageSize,
  })
  const { items, total } = toPage(response)
  return { items: items.map(toPastEventSummary), total }
}

/** Loads a whole event for its public page; resolves to `null` when it does not exist (404). */
export async function getEventRequest(id: string): Promise<EventDetail | null> {
  const event = await unwrapOrNull(getApiEventsEventId(id))
  return event ? toEventDetail(event) : null
}

/** Loads the signed-in user's decision state for every terms document linked to the event. */
export async function getEventTermsStateRequest(eventId: string): Promise<EventTermsState> {
  const { data } = await getApiEventsEventIdTerms(eventId)
  return toEventTermsState(data)
}

/**
 * Loads the event's activities that the signed-in user leads with a confirmed assignment, with
 * their confirmed attendees. The backend decides what the caller may see; anyone else gets `[]`.
 */
export async function getEventLeaderRosterRequest(
  eventId: string,
): Promise<readonly LeaderRosterActivity[]> {
  const { data } = await getApiEventsEventIdLeaderRoster(eventId)
  return data.map(toLeaderRosterActivity)
}

/**
 * Builds the home board: the featured event while it has not finished, plus up to three upcoming
 * events, excluding the featured one so it is not shown twice.
 */
export async function getHomeEventsRequest(): Promise<HomeEvents> {
  const [featuredPage, upcomingPage] = await Promise.all([
    getApiEvents({ featured: true, scope: 'Upcoming', pageSize: 1 }),
    getApiEvents({ scope: 'Upcoming', sort: 'eventStartsAt', pageSize: 4 }),
  ])
  const [first] = toPage(featuredPage).items
  const featured = first ? toEventSummary(first) : null
  const items = toPage(upcomingPage)
    .items.map(toEventSummary)
    .filter((event) => event.id !== featured?.id)
    .slice(0, 3)
  return { featured, items }
}

/** Fetches one page of the admin events table. */
export async function getEventsPageRequest(
  params: EventListParams,
): Promise<ServerTablePage<EventListing>> {
  const { items, total } = toPage(await getApiEvents(params))
  return { items: items.map(toEventListing), total }
}

/** Creates an event. */
export async function createEventRequest(input: EventInput): Promise<void> {
  await postApiEvents(toEventRequest(input))
}

/** Replaces an event's editable data. */
export async function updateEventRequest(id: string, input: EventInput): Promise<void> {
  await putApiEventsEventId(id, toEventRequest(input))
}

/** Deletes an event with its activities. */
export async function deleteEventRequest(id: string): Promise<void> {
  await deleteApiEventsEventId(id)
}

/** Makes the event the only featured one; featuring the featured event again changes nothing. */
export async function featureEventRequest(id: string): Promise<void> {
  await patchApiEventsEventIdFeature(id)
}
