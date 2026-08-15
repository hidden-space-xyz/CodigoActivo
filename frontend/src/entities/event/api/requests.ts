import {
  deleteApiEventsEventId,
  getApiEvents,
  getApiEventsEventId,
  getApiEventsEventIdRatings,
  getApiEventsEventIdTermsAcceptance,
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

import type { EventDetail, HomeEvents, PastEvent, UpcomingEvent } from '../model/types'
import { toEventDetail, toPastEvent, toUpcomingEvent } from './mapper'

export async function getUpcomingEventsPageRequest(
  page: number,
  pageSize: number,
): Promise<PagedListPage<UpcomingEvent>> {
  const result = await getApiEvents({ scope: 'Upcoming', sort: 'eventStartsAt', page, pageSize })
  const { items, total } = toPage(result)
  return { items: items.map(toUpcomingEvent), total }
}

export async function getPastEventYearsRequest(): Promise<readonly string[]> {
  const { data } = await getApiEventsPastYears()
  return (data ?? []).map(String)
}

export async function getPastEventsPageRequest(
  year: string,
  page: number,
  pageSize: number,
): Promise<PagedListPage<PastEvent>> {
  const result = await getApiEvents({
    scope: 'Past',
    year: Number(year),
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

export async function getEventByIdRequest(id: string): Promise<EventDetail | null> {
  const event = await unwrapOrNull<EventResponse>(getApiEventsEventId(id))
  return event ? toEventDetail(event) : null
}

export async function getEventTermsAcceptanceRequest(eventId: string): Promise<boolean> {
  const { data } = await getApiEventsEventIdTermsAcceptance(eventId)
  return data.accepted ?? false
}

export async function getHomeEventsRequest(): Promise<HomeEvents> {
  const [featured, upcomingPage] = await Promise.all([
    getFeaturedEventRequest(),
    getApiEvents({ scope: 'Upcoming', sort: 'eventStartsAt', pageSize: 4 }),
  ])
  const upcoming = (upcomingPage.data.items ?? []).map(toUpcomingEvent)
  const items = upcoming.filter((event) => event.id !== featured?.id).slice(0, 3)
  return { featured, items }
}

export function getEventsAdminPageRequest(
  params: GetApiEventsParams,
): Promise<{ items: EventListItemResponse[]; total: number }> {
  return getApiEvents(params).then(toPage)
}

export function getEventAdminRequest(id: string) {
  return unwrapOrNull<EventResponse>(getApiEventsEventId(id))
}

export function createEventRequest(body: CreateEventRequest) {
  return postApiEvents(body).then((r) => r.data)
}

export function updateEventRequest(id: string, body: UpdateEventRequest) {
  return putApiEventsEventId(id, body).then((r) => r.data)
}

export function deleteEventRequest(id: string) {
  return deleteApiEventsEventId(id)
}

export function toggleEventFeatureRequest(id: string) {
  return patchApiEventsEventIdFeature(id).then((r) => r.data)
}

export function getEventRatingsPageRequest(
  eventId: string,
  params: GetApiEventsEventIdRatingsParams,
) {
  return getApiEventsEventIdRatings(eventId, params).then(toPage)
}

export function getEventSummaryRequest(eventId: string) {
  return getApiReportsEventsEventIdSummary(eventId).then((r) => r.data)
}

export function getEventAttendeesPageRequest(
  eventId: string,
  params: GetApiReportsEventsEventIdAttendeesParams,
) {
  return getApiReportsEventsEventIdAttendees(eventId, params).then(toPage)
}

export function getEventBadgesRequest(eventId: string) {
  return getApiReportsEventsEventIdBadges(eventId).then((r) => r.data)
}

export function getEventRosterRequest(eventId: string) {
  return getApiReportsEventsEventIdRoster(eventId).then((r) => r.data)
}

export function getDashboardAnalyticsRequest(params: GetApiReportsDashboardAnalyticsParams) {
  return getApiReportsDashboardAnalytics(params).then((r) => r.data)
}
