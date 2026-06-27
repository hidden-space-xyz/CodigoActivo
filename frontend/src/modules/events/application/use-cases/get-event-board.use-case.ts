import type { UpcomingEvent } from '@/modules/events/domain/entities/event.entity'
import type { EventRepository } from '@/modules/events/domain/repositories/event-repository'

export async function getEventBoard(
  repository: EventRepository,
): Promise<readonly UpcomingEvent[]> {
  const upcoming = await repository.getUpcoming()
  return upcoming.filter((event) => !event.featured)
}
