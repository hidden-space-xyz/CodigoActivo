import type { ActivityRepository } from '@/modules/events/domain/repositories/activity-repository'

export function assignActivity(
  repository: ActivityRepository,
  activityId: string,
  userId: string,
  roleId: string,
): Promise<void> {
  return repository.assign(activityId, userId, roleId)
}
