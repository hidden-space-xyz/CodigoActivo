import type { HouseholdActivityAssignment } from '@/modules/events/domain/entities/activity.entity'
import type { ActivityRepository } from '@/modules/events/domain/repositories/activity-repository'

export function getHouseholdAssignments(
  repository: ActivityRepository,
  eventId: string,
): Promise<readonly HouseholdActivityAssignment[]> {
  return repository.getHouseholdAssignments(eventId)
}
