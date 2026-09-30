/** Guardian of a minor, printed on the minor's badge so staff can reach them. */
interface BadgeGuardian {
  readonly firstName: string
  /** Empty when the guardian has no phone. */
  readonly phone: string
}

/** An activity printed on a badge. */
interface BadgeActivity {
  readonly title: string
  readonly location: string
}

/** Badge of one attendee: name, user type and its color, activities and guardian for minors. */
export interface Badge {
  readonly userId: string
  readonly firstName: string
  readonly lastName: string
  readonly userTypeName: string
  readonly userTypeColor: string
  readonly guardian: BadgeGuardian | null
  readonly activities: readonly BadgeActivity[]
}

/** Badges of every attendee of an event, printed under the event's title. */
export interface EventBadges {
  readonly title: string
  readonly badges: readonly Badge[]
}
