import type { ActivityRepository } from '@/modules/events/domain/repositories/activity-repository'
import type { HouseholdAssignmentInput } from '@/modules/events/domain/value-objects/household-assignment-input'

export function assignHousehold(
  repository: ActivityRepository,
  activityId: string,
  assignments: readonly HouseholdAssignmentInput[],
): Promise<void> {
  return repository.assignHousehold(activityId, assignments)
}
