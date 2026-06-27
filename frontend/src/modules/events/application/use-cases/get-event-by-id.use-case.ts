import type { EventDetail } from '@/modules/events/domain/entities/event.entity'
import type { EventRepository } from '@/modules/events/domain/repositories/event-repository'

export function getEventById(
  repository: EventRepository,
  id: string,
): Promise<EventDetail | null> {
  return repository.getById(id)
}
