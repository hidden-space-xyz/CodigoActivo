import {
  getApiEvents,
  getApiEventsEventId,
  getApiEventsPastYears,
} from '@/shared/api/generated/endpoints/events/events'
import type { EventResponse } from '@/shared/api/generated/models'
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

export async function getHomeEventsRequest(): Promise<HomeEvents> {
  const [featured, upcomingPage] = await Promise.all([
    getFeaturedEventRequest(),
    getApiEvents({ scope: 'Upcoming', sort: 'eventStartsAt', pageSize: 4 }),
  ])
  const upcoming = (upcomingPage.data.items ?? []).map(toUpcomingEvent)
  const items = upcoming.filter((event) => event.id !== featured?.id).slice(0, 3)
  return { featured, items }
}
