import type { UpcomingEvent } from '@/modules/events/domain/entities/event.entity'
import type { EventRepository } from '@/modules/events/domain/repositories/event-repository'

export function getUpcomingEvents(repository: EventRepository): Promise<readonly UpcomingEvent[]> {
  return repository.getUpcoming()
}
