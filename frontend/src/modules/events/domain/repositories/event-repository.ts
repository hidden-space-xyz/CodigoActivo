import type {
  EventDetail,
  PastEvent,
  UpcomingEvent,
} from '@/modules/events/domain/entities/event.entity'

export interface EventRepository {
  getUpcoming(): Promise<readonly UpcomingEvent[]>
  getPast(): Promise<readonly PastEvent[]>
  getFeatured(): Promise<UpcomingEvent | null>
  getById(id: string): Promise<EventDetail | null>
}
