import type { OverlapCheck } from '@/modules/events/domain/entities/activity.entity'
import type { ActivityRepository } from '@/modules/events/domain/repositories/activity-repository'

export function verifyOverlaps(
  repository: ActivityRepository,
  activityId: string,
  userId: string,
): Promise<OverlapCheck> {
  return repository.verifyOverlaps(activityId, userId)
}
