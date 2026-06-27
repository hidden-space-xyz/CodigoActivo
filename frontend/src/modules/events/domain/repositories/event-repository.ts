import type {
  EventDetail,
  PastEvent,
  UpcomingEvent,
} from '@/modules/events/domain/entities/event.entity'

export interface EventRepository {
  getUpcoming(): Promise<readonly UpcomingEvent[]>
  getPast(): Promise<readonly PastEvent[]>
  getById(id: string): Promise<EventDetail | null>
}
