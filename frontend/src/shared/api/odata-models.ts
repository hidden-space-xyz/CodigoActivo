/**
 * Read-model types for OData endpoints whose DTOs are no longer part of the generated client
 * (they were only ever returned by the now-removed REST GET actions). Shapes mirror the backend
 * response records exactly (camelCase JSON).
 */

export interface UserTypeResponse {
  readonly id: string
  readonly name: string
  readonly description: string
  readonly color: string
}

export interface AssignmentStatusTypeResponse {
  readonly id: string
  readonly name: string
  readonly description: string
  readonly color: string
}

export interface RegistrationTypeResponse {
  readonly id: string
  readonly name: string
  readonly description: string
  readonly color: string
  readonly isAllowedForMinors: boolean
  readonly isAllowedForAdults: boolean
}

export interface AssignedActivityRoleResponse {
  readonly id: string
  readonly name: string
}

export interface AssignedActivityStatusResponse {
  readonly id: string
  readonly name: string
}

export interface AssignedActivityResponse {
  readonly activityId: string
  readonly title: string
  readonly description: string
  readonly activityStartsAt: string
  readonly activityEndsAt: string
  readonly eventId: string
  readonly roleType: AssignedActivityRoleResponse
  readonly status: AssignedActivityStatusResponse
}

// --- Computed reads (OData unbound functions) ---

export interface EventRoleTypeSummaryResponse {
  readonly roleTypeId: string
  readonly roleTypeName: string | null
  readonly approvedAssignments: number
}

export interface EventSummaryResponse {
  readonly eventId: string
  readonly title: string
  readonly activitiesCount: number
  readonly totalAssignments: number
  readonly requestedAssignments: number
  readonly confirmedAssignments: number
  readonly deniedAssignments: number
  readonly distinctVolunteers: number
  readonly roleTypeBreakdown: EventRoleTypeSummaryResponse[]
}

export interface ActivityRoleTypeSummaryResponse {
  readonly roleTypeId: string
  readonly roleTypeName: string | null
  readonly approvedAssignments: number
}

export interface ActivityAssignmentRowResponse {
  readonly userId: string
  readonly firstName: string | null
  readonly lastName: string | null
  readonly email: string | null
  readonly phone: string | null
  readonly parentId: string | null
  readonly signedUp: boolean
  readonly roleTypeId: string | null
  readonly roleTypeName: string | null
  readonly statusId: string | null
  readonly statusName: string | null
}

export interface ActivityAssignmentsReportResponse {
  readonly activityId: string
  readonly title: string
  readonly totalSignups: number
  readonly roleTypeBreakdown: ActivityRoleTypeSummaryResponse[]
  readonly rows: ActivityAssignmentRowResponse[]
}

export interface DashboardSummaryResponse {
  readonly events: number
  readonly activities: number
  readonly resources: number
  readonly announcements: number
  readonly partners: number
  readonly users: number
}

export interface OverlappingActivityResponse {
  readonly activityId: string
  readonly title: string
  readonly startsAt: string
  readonly endsAt: string
}

export interface TimeOverlapResponse {
  readonly hasOverlaps: boolean
  readonly overlaps: OverlappingActivityResponse[]
}

export interface HouseholdMemberAssignmentResponse {
  readonly activityId: string
  readonly userId: string
  readonly firstName: string
  readonly lastName: string
  readonly roleTypeId: string
  readonly roleName: string
  readonly statusId: string
  readonly statusName: string
}
