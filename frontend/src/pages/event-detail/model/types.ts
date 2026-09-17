/** A household member's enrollment in one activity, as listed on its timeline card. */
export interface TimelineMemberAssignment {
  userId: string
  name: string
  roleName: string
  status: string
}

/**
 * Public event activity merged with the signed-in user's enrollment data, as rendered by the
 * activities timeline.
 */
export interface TimelineActivity {
  id: string
  title: string
  description: string
  location: string
  modality: string
  /** `null` when the activity has no schedule; it is then listed apart from the timeline. */
  start: Date | null
  end: Date | null
  /** High-demand roles, which show a warning when chosen; empty until enrollments load. */
  highDemandRoleIds: string[]
  /** The current user's own enrollment, or `null` when not enrolled. */
  assignment: { status: string; roleName: string } | null
  household: TimelineMemberAssignment[]
}
