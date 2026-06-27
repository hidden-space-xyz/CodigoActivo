import type { UpcomingEvent } from '@/modules/events/domain/entities/event.entity'
import type { EventRepository } from '@/modules/events/domain/repositories/event-repository'

export interface HomeEvents {
  /** Highlighted event: nearest upcoming/ongoing, or the most recent past one. */
  readonly featured: UpcomingEvent | null
  /** Up to 3 nearest upcoming/ongoing events, excluding the featured one. */
  readonly items: readonly UpcomingEvent[]
}

export async function getHomeEvents(repository: EventRepository): Promise<HomeEvents> {
  const [featured, upcoming] = await Promise.all([
    repository.getFeatured(),
    repository.getUpcoming(),
  ])
  const items = upcoming.filter((event) => event.id !== featured?.id).slice(0, 3)
  return { featured, items }
}
