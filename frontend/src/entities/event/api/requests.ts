import {
  getApiEvents,
  getApiEventsEventId,
  getApiEventsPastYears,
} from '@/shared/api/generated/endpoints/events/events'
import type { EventListItemResponse, EventResponse } from '@/shared/api/generated/models'
import { FEATURED_FIRST_SORT, fetchAllPages, toPage, unwrapOrNull } from '@/shared/api'

import type { EventDetail, HomeEvents, PastEvent, UpcomingEvent } from '../model/types'
import { toEventDetail, toPastEvent, toUpcomingEvent } from './mapper'

export async function getUpcomingEventsRequest(): Promise<readonly UpcomingEvent[]> {
  const items = await fetchAllPages<EventListItemResponse>((page, pageSize) =>
    getApiEvents({ scope: 'Upcoming', sort: 'eventStartsAt', page, pageSize }).then(toPage),
  )
  return items.map(toUpcomingEvent)
}

export async function getPastEventYearsRequest(): Promise<readonly string[]> {
  const { data } = await getApiEventsPastYears()
  return (data ?? []).map(String)
}

export async function getPastEventsRequest(year: string): Promise<readonly PastEvent[]> {
  const items = await fetchAllPages<EventListItemResponse>((page, pageSize) =>
    getApiEvents({
      scope: 'Past',
      year: Number(year),
      sort: '-eventStartsAt',
      page,
      pageSize,
    }).then(toPage),
  )
  return items.map(toPastEvent)
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
