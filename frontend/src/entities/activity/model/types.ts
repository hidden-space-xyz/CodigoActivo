export interface ActivityRole {
  readonly id: string
  readonly name: string
}

export interface HouseholdSignupRoles {
  readonly userId: string
  readonly roles: readonly ActivityRole[]
}

export interface EventActivity {
  readonly id: string
  readonly title: string
  readonly description: string
  readonly location: string
  readonly modality: string
  readonly startsAt: string | null
  readonly endsAt: string | null
  readonly highDemandRoleIds: readonly string[]
}

export interface ActivityRoleCapacity {
  readonly roleTypeId: string
  readonly desiredCount: number | null
}

export interface ActivityDetail {
  readonly id: string
  readonly title: string
  readonly description: string
  readonly location: string
  readonly modalityId: string
  readonly startsAt: string | null
  readonly endsAt: string | null
  readonly thumbnailId: string
  readonly roleCapacities: readonly ActivityRoleCapacity[]
}

export interface ActivityAssignment {
  readonly activityId: string
  readonly status: string
  readonly roleName: string
}

export interface HouseholdActivityAssignment {
  readonly activityId: string
  readonly userId: string
  readonly name: string
  readonly roleName: string
  readonly status: string
}

export interface HouseholdMember {
  readonly id: string
  readonly name: string
}

export interface ActivityOverlap {
  readonly activityId: string
  readonly title: string
  readonly startsAt: string | null
  readonly endsAt: string | null
}

export interface OverlapCheck {
  readonly hasOverlaps: boolean
  readonly overlaps: readonly ActivityOverlap[]
}
