import type {
  ActivityAssignment,
  EventActivity,
  HouseholdActivityAssignment,
  HouseholdMember,
  OverlapCheck,
} from '@/modules/events/domain/entities/activity.entity'
import type { ActivityRepository } from '@/modules/events/domain/repositories/activity-repository'
import type { HouseholdAssignmentInput } from '@/modules/events/domain/value-objects/household-assignment-input'
import {
  toActivityAssignment,
  toEventActivity,
  toHouseholdActivityAssignment,
  toHouseholdMember,
  toOverlapCheck,
} from '@/modules/events/infrastructure/mappers/activity.mapper'
import {
  getApiActivitiesActivityIdUserIdVerifyTimeOverlaps,
  getApiActivitiesAssigned,
  getApiActivitiesEventId,
  getApiActivitiesEventIdHouseholdAssignments,
  patchApiActivitiesActivityIdUserIdAssign,
  patchApiActivitiesActivityIdUserIdUnassign,
  postApiActivitiesActivityIdAssignHousehold,
} from '@/shared/api/generated/endpoints/activities/activities'
import { getApiUsersUserIdChildren } from '@/shared/api/generated/endpoints/users/users'

export class HttpActivityRepository implements ActivityRepository {
  async getByEvent(eventId: string): Promise<readonly EventActivity[]> {
    const response = await getApiActivitiesEventId(eventId)
    return (response.data ?? []).map(toEventActivity)
  }

  async getMyAssignments(): Promise<readonly ActivityAssignment[]> {
    const response = await getApiActivitiesAssigned()
    return (response.data ?? []).map(toActivityAssignment)
  }

  async getHouseholdAssignments(eventId: string): Promise<readonly HouseholdActivityAssignment[]> {
    const response = await getApiActivitiesEventIdHouseholdAssignments(eventId)
    return (response.data ?? []).map(toHouseholdActivityAssignment)
  }

  async getHouseholdMembers(userId: string): Promise<readonly HouseholdMember[]> {
    const response = await getApiUsersUserIdChildren(userId)
    return (response.data ?? []).map(toHouseholdMember)
  }

  async verifyOverlaps(activityId: string, userId: string): Promise<OverlapCheck> {
    const response = await getApiActivitiesActivityIdUserIdVerifyTimeOverlaps(activityId, userId)
    return toOverlapCheck(response.data)
  }

  async assign(activityId: string, userId: string, roleId: string): Promise<void> {
    await patchApiActivitiesActivityIdUserIdAssign(activityId, userId, {
      activityRoleTypeId: roleId,
    })
  }

  async assignHousehold(
    activityId: string,
    assignments: readonly HouseholdAssignmentInput[],
  ): Promise<void> {
    await postApiActivitiesActivityIdAssignHousehold(activityId, {
      assignments: assignments.map((assignment) => ({
        userId: assignment.userId,
        activityRoleTypeId: assignment.roleId,
      })),
    })
  }

  async unassign(activityId: string, userId: string): Promise<void> {
    await patchApiActivitiesActivityIdUserIdUnassign(activityId, userId)
  }
}
