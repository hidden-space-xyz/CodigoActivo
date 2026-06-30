export interface ActivityRole {
  readonly id: string
  readonly name: string
}

export interface EventActivity {
  readonly id: string
  readonly title: string
  readonly description: string
  readonly startsAt: string | null
  readonly endsAt: string | null
  readonly roles: readonly ActivityRole[]
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
