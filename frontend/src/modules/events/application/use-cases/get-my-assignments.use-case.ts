import type { ActivityAssignment } from '@/modules/events/domain/entities/activity.entity'
import type { ActivityRepository } from '@/modules/events/domain/repositories/activity-repository'

export function getMyAssignments(
  repository: ActivityRepository,
): Promise<readonly ActivityAssignment[]> {
  return repository.getMyAssignments()
}
