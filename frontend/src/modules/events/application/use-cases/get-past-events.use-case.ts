import type { PastEvent } from '@/modules/events/domain/entities/event.entity'
import type { EventRepository } from '@/modules/events/domain/repositories/event-repository'

export function getPastEvents(repository: EventRepository): Promise<readonly PastEvent[]> {
  return repository.getPast()
}
