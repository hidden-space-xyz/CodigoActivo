/** Participation role (activity role type) a user can take in an activity. */
export interface ActivityRole {
  readonly id: string
  readonly name: string
}

/** One decision on an event terms document, sent alongside a signup for undecided documents. */
export interface TermsDecisionInput {
  readonly termsDocumentId: string
  readonly accepted: boolean
}

/** Roles one household member may sign up for, as allowed by their user type. */
export interface HouseholdSignupRoles {
  readonly userId: string
  readonly roles: readonly ActivityRole[]
}

/** Activity shown on an event's public timeline, mapped from `ActivityResponse`. */
export interface EventActivity {
  readonly id: string
  readonly title: string
  readonly description: string
  readonly location: string
  readonly modality: string
  readonly startsAt: string | null
  readonly endsAt: string | null
  /** Roles whose non-denied signups exceed the desired count; the UI warns before joining them. */
  readonly highDemandRoleIds: readonly string[]
}

interface ActivityRoleCapacity {
  readonly roleTypeId: string
  readonly desiredCount: number | null
}

/** Editable activity data used to prefill the admin activity form. */
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

/** The signed-in user's own signup to an activity, with display-ready status and role names. */
export interface ActivityAssignment {
  readonly activityId: string
  readonly status: string
  readonly roleName: string
}

/** Signup of the user or one of their minors to an activity of the current event. */
export interface HouseholdActivityAssignment {
  readonly activityId: string
  readonly userId: string
  readonly name: string
  readonly roleName: string
  readonly status: string
}

/** Person the signed-in user can sign up: themselves or one of their minors. */
export interface HouseholdMember {
  readonly id: string
  readonly name: string
}

/** Activity the user is already assigned to whose schedule collides with the one being joined. */
export interface ActivityOverlap {
  readonly activityId: string
  readonly title: string
  readonly startsAt: string | null
  readonly endsAt: string | null
}

/** Result of the schedule-conflict check shown as a warning before confirming a signup. */
export interface OverlapCheck {
  readonly hasOverlaps: boolean
  readonly overlaps: readonly ActivityOverlap[]
}
