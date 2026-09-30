/** Guardian of a minor participant; contact fields are empty when unknown. */
interface RosterGuardian {
  readonly firstName: string
  readonly lastName: string
  readonly email: string
  readonly phone: string
  readonly secondaryPhone: string
}

/** Confirmed participant of an activity; contact fields are empty when unknown. */
export interface RosterParticipant {
  readonly userId: string
  readonly firstName: string
  readonly lastName: string
  readonly birthDate: string | null
  readonly email: string
  readonly phone: string
  readonly secondaryPhone: string
  readonly roleName: string
  readonly guardian: RosterGuardian | null
}

/** Activity of the roster with its participants, grouped by role as the API orders them. */
export interface RosterActivity {
  readonly id: string
  readonly title: string
  readonly location: string
  readonly startsAt: string
  readonly endsAt: string
  readonly participants: readonly RosterParticipant[]
}

/** Printable attendance roster of an event: its activities and their participants. */
export interface EventRoster {
  readonly title: string
  readonly activities: readonly RosterActivity[]
}
