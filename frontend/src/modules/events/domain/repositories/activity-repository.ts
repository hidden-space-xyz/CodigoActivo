import type {
  ActivityAssignment,
  EventActivity,
  HouseholdActivityAssignment,
  HouseholdMember,
  OverlapCheck,
} from '@/modules/events/domain/entities/activity.entity'
import type { HouseholdAssignmentInput } from '@/modules/events/domain/value-objects/household-assignment-input'

export interface ActivityRepository {
  getByEvent(eventId: string): Promise<readonly EventActivity[]>
  getMyAssignments(): Promise<readonly ActivityAssignment[]>
  getHouseholdAssignments(eventId: string): Promise<readonly HouseholdActivityAssignment[]>
  getHouseholdMembers(userId: string): Promise<readonly HouseholdMember[]>
  verifyOverlaps(activityId: string, userId: string): Promise<OverlapCheck>
  assign(activityId: string, userId: string, roleId: string): Promise<void>
  assignHousehold(
    activityId: string,
    assignments: readonly HouseholdAssignmentInput[],
  ): Promise<void>
  unassign(activityId: string, userId: string): Promise<void>
}
