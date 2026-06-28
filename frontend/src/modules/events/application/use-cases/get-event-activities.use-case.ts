import type { EventActivity } from '@/modules/events/domain/entities/activity.entity'
import type { ActivityRepository } from '@/modules/events/domain/repositories/activity-repository'

export function getEventActivities(
  repository: ActivityRepository,
  eventId: string,
): Promise<readonly EventActivity[]> {
  return repository.getByEvent(eventId)
}
