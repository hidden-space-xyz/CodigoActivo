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
