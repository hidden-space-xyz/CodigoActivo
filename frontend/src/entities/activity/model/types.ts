/** Participation role (activity role type) a user can take in an activity. */
export interface ActivityRole {
  readonly id: string
  readonly name: string
}

/** Status an activity signup can be in, with the color its tag is shown in. */
export interface AssignmentStatus {
  readonly id: string
  readonly name: string
  readonly color: string
}

/** Way an activity is offered, such as on site or online. */
export interface ActivityModality {
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
  readonly startsAt: string
  readonly endsAt: string
  /** Roles whose non-denied signups exceed the desired count; the UI warns before joining them. */
  readonly highDemandRoleIds: readonly string[]
}

/** How many people an activity would like to have in one role. */
interface ActivityRoleCapacity {
  readonly roleTypeId: string
  readonly desiredCount: number
}

/** Editable activity data used to prefill the admin activity form. */
export interface ActivityDetail {
  readonly id: string
  readonly title: string
  readonly description: string
  readonly location: string
  readonly modalityId: string
  readonly startsAt: string
  readonly endsAt: string
  readonly thumbnailId: string
  readonly roleCapacities: readonly ActivityRoleCapacity[]
}

/** An activity as the admin activities table of an event lists it; `modality` is its name. */
export interface ActivityListing {
  readonly id: string
  readonly title: string
  readonly location: string
  readonly modality: string
  readonly startsAt: string
  readonly endsAt: string
  readonly thumbnailId: string
}

/** Values an activity is created or replaced with; roles without a desired count are left out. */
export interface ActivityInput {
  readonly title: string
  readonly description: string
  readonly location: string
  readonly modalityId: string
  readonly startsAt: string
  readonly endsAt: string
  readonly thumbnailId: string
  readonly roleCapacities: readonly ActivityRoleCapacity[]
}

/** Filters, sort and page of the admin activities table. */
export interface ActivityListParams {
  readonly eventId?: string
  readonly title?: string
  readonly modalityTypeId?: string
  readonly activityDateFrom?: string
  readonly activityDateTo?: string
  readonly page?: number
  readonly pageSize?: number
  readonly sort?: string
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
  readonly startsAt: string
  readonly endsAt: string
}

/** Result of the schedule-conflict check shown as a warning before confirming a signup. */
export interface OverlapCheck {
  readonly hasOverlaps: boolean
  readonly overlaps: readonly ActivityOverlap[]
}
