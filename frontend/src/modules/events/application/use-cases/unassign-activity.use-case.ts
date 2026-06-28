import type { ActivityRepository } from '@/modules/events/domain/repositories/activity-repository'

export function unassignActivity(
  repository: ActivityRepository,
  activityId: string,
  userId: string,
): Promise<void> {
  return repository.unassign(activityId, userId)
}
