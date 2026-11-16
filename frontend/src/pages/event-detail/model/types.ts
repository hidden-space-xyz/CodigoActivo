/** A household member's signup to one activity, as listed on its timeline card. */
export interface TimelineMemberAssignment {
  userId: string
  name: string
  roleName: string
  status: string
}

/**
 * Public event activity merged with the signups of the signed-in user and their household, as
 * rendered by the activities timeline.
 */
export interface TimelineActivity {
  id: string
  title: string
  description: string
  location: string
  modality: string
  start: Date
  end: Date
  /** The activity has already begun, so signups and withdrawals are closed for it. */
  started: boolean
  /** High-demand roles, which show a warning when chosen; empty until signups load. */
  highDemandRoleIds: string[]
  /** The current user's own signup, or `null` when not signed up. */
  assignment: { status: string; roleName: string } | null
  /** Signups to this activity of the user and of the minors in their care. */
  household: TimelineMemberAssignment[]
}
