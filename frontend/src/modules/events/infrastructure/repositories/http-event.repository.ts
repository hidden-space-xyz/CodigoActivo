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

export class HttpEventRepository implements EventRepository {
  private async fetchAll(): Promise<EventResponse[]> {
    const response = await getApiEvents()
    return response.data ?? []
  }

  async getUpcoming(): Promise<readonly UpcomingEvent[]> {
    const today = startOfToday()
    const upcoming = (await this.fetchAll())
      .filter((event) => {
        const start = startsAt(event)
        return start === null || start >= today
      })
      .sort((a, b) => (startsAt(a) ?? Infinity) - (startsAt(b) ?? Infinity))
    return upcoming.map((event, index) => toUpcomingEvent(event, index === 0))
  }

  async getPast(): Promise<readonly PastEvent[]> {
    const today = startOfToday()
    return (await this.fetchAll())
      .filter((event) => {
        const start = startsAt(event)
        return start !== null && start < today
      })
      .sort((a, b) => (startsAt(b) ?? 0) - (startsAt(a) ?? 0))
      .map(toPastEvent)
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
