import type {
  EventDetail,
  PastEvent,
  UpcomingEvent,
} from '@/modules/events/domain/entities/event.entity'
import type { EventRepository } from '@/modules/events/domain/repositories/event-repository'
import {
  toEventDetail,
  toPastEvent,
  toUpcomingEvent,
} from '@/modules/events/infrastructure/mappers/event.mapper'
import { getApiEvents, getApiEventsEventId } from '@/shared/api/generated/endpoints/events/events'
import { ApiError } from '@/shared/api/http-client'
import type { EventResponse } from '@/shared/api/generated/models'

function startOfToday(): number {
  const now = new Date()
  now.setHours(0, 0, 0, 0)
  return now.getTime()
}

function startsAt(event: EventResponse): number | null {
  return event.eventStartsAt ? new Date(event.eventStartsAt).getTime() : null
}

/** Upcoming OR ongoing: the event has not finished yet. */
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

export class HttpEventRepository implements EventRepository {
  private async fetchAll(): Promise<EventResponse[]> {
    const response = await getApiEvents()
    return response.data ?? []
  }

  async getUpcoming(): Promise<readonly UpcomingEvent[]> {
    const upcoming = (await this.fetchAll()).filter(isUpcomingOrOngoing).sort(byStartAscending)
    return upcoming.map(toUpcomingEvent)
  }

  async getPast(): Promise<readonly PastEvent[]> {
    return (await this.fetchAll()).filter(isFinished).sort(byStartDescending).map(toPastEvent)
  }

  async getFeatured(): Promise<UpcomingEvent | null> {
    const all = await this.fetchAll()
    const flagged = all.find((event) => event.featured)
    if (flagged) return toUpcomingEvent(flagged)
    // Fallback: the most recently created event when none is explicitly featured.
    const mostRecent = all
      .slice()
      .sort((a, b) => (b.createdAt ?? '').localeCompare(a.createdAt ?? ''))[0]
    return mostRecent ? toUpcomingEvent(mostRecent) : null
  }

  async getById(id: string): Promise<EventDetail | null> {
    try {
      const response = await getApiEventsEventId(id)
      return toEventDetail(response.data)
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) return null
      throw error
    }
  }
}
