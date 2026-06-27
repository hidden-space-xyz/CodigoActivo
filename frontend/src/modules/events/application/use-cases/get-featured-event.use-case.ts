import type { UpcomingEvent } from '@/modules/events/domain/entities/event.entity'
import type { EventRepository } from '@/modules/events/domain/repositories/event-repository'

export async function getFeaturedEvent(repository: EventRepository): Promise<UpcomingEvent | null> {
  const upcoming = await repository.getUpcoming()
  return upcoming.find((event) => event.featured) ?? upcoming[0] ?? null
}
