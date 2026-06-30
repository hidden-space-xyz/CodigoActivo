import { getApiEvents, getApiEventsEventId } from '@/shared/api/generated/endpoints/events/events'
import type { EventResponse } from '@/shared/api/generated/models'
import { ApiError } from '@/shared/api'

import type { EventDetail, HomeEvents, PastEvent, UpcomingEvent } from '../model/types'
import { toEventDetail, toPastEvent, toUpcomingEvent } from './mapper'

function startOfToday(): number {
  const now = new Date()
  now.setHours(0, 0, 0, 0)
  return now.getTime()
}

function startsAt(event: EventResponse): number | null {
  return event.eventStartsAt ? new Date(event.eventStartsAt).getTime() : null
}

function isUpcomingOrOngoing(event: EventResponse): boolean {
  const end = event.eventEndsAt ? new Date(event.eventEndsAt).getTime() : null
  if (end !== null) return end >= Date.now()
  const start = startsAt(event)
  if (start !== null) return start >= startOfToday()
  return true
}

function isFinished(event: EventResponse): boolean {
  const end = event.eventEndsAt ? new Date(event.eventEndsAt).getTime() : null
  if (end !== null) return end < Date.now()
  const start = startsAt(event)
  if (start !== null) return start < startOfToday()
  return false
}

const byStartAscending = (a: EventResponse, b: EventResponse): number =>
  (startsAt(a) ?? Infinity) - (startsAt(b) ?? Infinity)

const byStartDescending = (a: EventResponse, b: EventResponse): number =>
  (startsAt(b) ?? 0) - (startsAt(a) ?? 0)

async function fetchAll(): Promise<EventResponse[]> {
  const response = await getApiEvents()
  return response.data ?? []
}

export async function getUpcomingEventsRequest(): Promise<readonly UpcomingEvent[]> {
  const upcoming = (await fetchAll()).filter(isUpcomingOrOngoing).sort(byStartAscending)
  return upcoming.map(toUpcomingEvent)
}

export async function getPastEventsRequest(): Promise<readonly PastEvent[]> {
  return (await fetchAll()).filter(isFinished).sort(byStartDescending).map(toPastEvent)
}

export async function getFeaturedEventRequest(): Promise<UpcomingEvent | null> {
  const all = await fetchAll()
  const flagged = all.find((event) => event.featured)
  if (flagged) return toUpcomingEvent(flagged)
  const mostRecent = all
    .slice()
    .sort((a, b) => (b.createdAt ?? '').localeCompare(a.createdAt ?? ''))[0]
  return mostRecent ? toUpcomingEvent(mostRecent) : null
}

export async function getEventByIdRequest(id: string): Promise<EventDetail | null> {
  try {
    const response = await getApiEventsEventId(id)
    return toEventDetail(response.data)
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return null
    throw error
  }
}

export async function getHomeEventsRequest(): Promise<HomeEvents> {
  const [featured, upcoming] = await Promise.all([
    getFeaturedEventRequest(),
    getUpcomingEventsRequest(),
  ])
  const items = upcoming.filter((event) => event.id !== featured?.id).slice(0, 3)
  return { featured, items }
}
