import type {
  ActivityModalityTypeResponse,
  ActivityResponse,
  ActivityRoleTypeResponse,
  AssignedActivityResponse,
  AssignmentStatusTypeResponse,
  HouseholdMemberAssignmentResponse,
} from '@/shared/api/generated/models'

import { AUDIT } from './common'
import { EVENT_ID, THUMBNAIL_ID } from './events'

/** Activity of the default event with one volunteer capacity. */
export function buildActivityResponse(overrides: Partial<ActivityResponse> = {}): ActivityResponse {
  return {
    id: 'act-1',
    title: 'Robotics',
    description: 'Build robots',
    location: 'Room 1',
    activityStartsAt: '2026-10-10T09:00:00.000Z',
    activityEndsAt: '2026-10-10T11:00:00.000Z',
    eventId: EVENT_ID,
    modalityId: 'mod-1',
    modalityName: 'On site',
    thumbnailId: THUMBNAIL_ID,
    roleCapacities: [{ activityRoleTypeId: 'role-1', desiredCount: 4, isHighDemand: false }],
    ...AUDIT,
    updatedAt: null,
    ...overrides,
  }
}

/** Activity modalities catalog. */
export const modalityTypes: ActivityModalityTypeResponse[] = [
  { id: 'mod-1', name: 'On site' },
  { id: 'mod-2', name: 'Online' },
]

/** Activity role types catalog. */
export const roleTypes: ActivityRoleTypeResponse[] = [
  { id: 'role-1', name: 'Volunteer', description: '' },
  { id: 'role-2', name: 'Mentor', description: '' },
]

/** Signup status catalog. */
export const assignmentStatusTypes: AssignmentStatusTypeResponse[] = [
  { id: 'status-1', name: 'Requested', description: '', color: '#aabbcc' },
  { id: 'status-2', name: 'Confirmed', description: '', color: '#00ff00' },
]

/** The signed-in user's requested signup, as a participant, to the default activity. */
export function buildAssignedActivity(
  overrides: Partial<AssignedActivityResponse> = {},
): AssignedActivityResponse {
  return {
    activityId: 'act-1',
    title: 'Robotics',
    description: 'Build robots',
    activityStartsAt: '2026-10-10T09:00:00.000Z',
    activityEndsAt: '2026-10-10T11:00:00.000Z',
    eventId: EVENT_ID,
    roleType: { id: 'role-participant', name: 'Participante' },
    status: { id: 'status-1', name: 'Solicitada' },
    ...overrides,
  }
}

/** A minor's requested signup, as a participant, to the default activity. */
export function buildHouseholdAssignment(
  overrides: Partial<HouseholdMemberAssignmentResponse> = {},
): HouseholdMemberAssignmentResponse {
  return {
    activityId: 'act-1',
    userId: 'child-1',
    firstName: 'Byron',
    lastName: 'King',
    roleTypeId: 'role-participant',
    roleName: 'Participante',
    statusId: 'status-1',
    statusName: 'Solicitada',
    ...overrides,
  }
}
