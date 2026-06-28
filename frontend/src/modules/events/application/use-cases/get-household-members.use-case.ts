import type { HouseholdMember } from '@/modules/events/domain/entities/activity.entity'
import type { ActivityRepository } from '@/modules/events/domain/repositories/activity-repository'

export function getHouseholdMembers(
  repository: ActivityRepository,
  userId: string,
): Promise<readonly HouseholdMember[]> {
  return repository.getHouseholdMembers(userId)
}
