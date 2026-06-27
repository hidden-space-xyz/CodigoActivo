import type { PastEvent, UpcomingEvent } from '@/modules/events/domain/entities/event.entity'

export interface EventRepository {
  getUpcoming(): Promise<readonly UpcomingEvent[]>
  getPast(): Promise<readonly PastEvent[]>
  getById(id: string): Promise<UpcomingEvent | null>
}
