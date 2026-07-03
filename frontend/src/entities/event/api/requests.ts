import type { EventResponse } from '@/shared/api/generated/models'
import { combineFilters, fetchODataEntity, fetchODataList, odataInt } from '@/shared/api'

import type { EventDetail, HomeEvents, PastEvent, UpcomingEvent } from '../model/types'
import { toEventDetail, toPastEvent, toUpcomingEvent } from './mapper'

const EVENTS = 'Events'

function today(): string {
  const now = new Date()
  const year = now.getFullYear()
  const month = String(now.getMonth() + 1).padStart(2, '0')
  const day = String(now.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

export async function getUpcomingEventsRequest(): Promise<readonly UpcomingEvent[]> {
  const { items } = await fetchODataList<EventResponse>(EVENTS, {
    filter: `eventEndsAt ge ${today()}`,
    orderBy: 'eventStartsAt asc',
    top: 100,
  })
  return items.map(toUpcomingEvent)
}

export async function getPastEventYearsRequest(): Promise<readonly string[]> {
  const { items } = await fetchODataList<EventResponse>(EVENTS, {
    filter: `eventEndsAt lt ${today()}`,
    orderBy: 'eventStartsAt desc',
    select: 'eventStartsAt',
    top: 100,
  })
  const years = new Set(
    items
      .map((event) => event.eventStartsAt)
      .filter((value): value is string => Boolean(value))
      .map((value) => value.slice(0, 4)),
  )
  return [...years].sort((a, b) => Number(b) - Number(a))
}

export async function getPastEventsRequest(year: string): Promise<readonly PastEvent[]> {
  const { items } = await fetchODataList<EventResponse>(EVENTS, {
    filter: combineFilters(`eventEndsAt lt ${today()}`, `year(eventStartsAt) eq ${odataInt(year)}`),
    orderBy: 'eventStartsAt desc',
    top: 500,
  })
  return items.map(toPastEvent)
}

async function getFeaturedEventRequest(): Promise<UpcomingEvent | null> {
  const flagged = await fetchODataList<EventResponse>(EVENTS, {
    filter: 'featured eq true',
    top: 1,
  })
  const [flaggedFirst] = flagged.items
  if (flaggedFirst) return toUpcomingEvent(flaggedFirst)

  const mostRecent = await fetchODataList<EventResponse>(EVENTS, {
    orderBy: 'createdAt desc',
    top: 1,
  })
  const [recentFirst] = mostRecent.items
  return recentFirst ? toUpcomingEvent(recentFirst) : null
}

export async function getEventByIdRequest(id: string): Promise<EventDetail | null> {
  const event = await fetchODataEntity<EventResponse>(EVENTS, id)
  return event ? toEventDetail(event) : null
}

export async function getHomeEventsRequest(): Promise<HomeEvents> {
  const [featured, upcomingPage] = await Promise.all([
    getFeaturedEventRequest(),
    fetchODataList<EventResponse>(EVENTS, {
      filter: `eventEndsAt ge ${today()}`,
      orderBy: 'eventStartsAt asc',
      top: 4,
    }),
  ])
  const upcoming = upcomingPage.items.map(toUpcomingEvent)
  const items = upcoming.filter((event) => event.id !== featured?.id).slice(0, 3)
  return { featured, items }
}
