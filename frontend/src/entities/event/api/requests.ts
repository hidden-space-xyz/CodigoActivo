import {
  getApiEvents,
  getApiEventsEventId,
  getApiEventsPastYears,
} from '@/shared/api/generated/endpoints/events/events'
import type { EventResponse } from '@/shared/api/generated/models'
import { fetchAllPages, unwrapOrNull } from '@/shared/api'

import type { EventDetail, HomeEvents, PastEvent, UpcomingEvent } from '../model/types'
import { toEventDetail, toPastEvent, toUpcomingEvent } from './mapper'

export async function getUpcomingEventsRequest(): Promise<readonly UpcomingEvent[]> {
  const { data } = await getApiEvents({ scope: 'Upcoming', sort: 'eventStartsAt', pageSize: 100 })
  return (data.items ?? []).map(toUpcomingEvent)
}

export async function getPastEventYearsRequest(): Promise<readonly string[]> {
  const { data } = await getApiEventsPastYears()
  return (data ?? []).map(String)
}

export async function getPastEventsRequest(year: string): Promise<readonly PastEvent[]> {
  const items = await fetchAllPages<EventResponse>((page, pageSize) =>
    getApiEvents({ scope: 'Past', year: Number(year), sort: '-eventStartsAt', page, pageSize }).then(
      (r) => ({ items: r.data.items ?? [], total: r.data.total ?? 0 }),
    ),
  )
  return items.map(toPastEvent)
}

async function getFeaturedEventRequest(): Promise<UpcomingEvent | null> {
  const flagged = await getApiEvents({ featured: true, pageSize: 1 })
  const flaggedFirst = flagged.data.items?.[0]
  if (flaggedFirst) return toUpcomingEvent(flaggedFirst)

  const mostRecent = await getApiEvents({ sort: '-createdAt', pageSize: 1 })
  const recentFirst = mostRecent.data.items?.[0]
  return recentFirst ? toUpcomingEvent(recentFirst) : null
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
