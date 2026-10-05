/** Colored category label shown on event cards; `color` is the CSS color set by admins. */
export interface EventCategoryTag {
  readonly id: string
  readonly name: string
  readonly color: string
}

/** Stage of an event, as the API computes it from its signup window and dates; drives styling. */
export type EventStatusKind =
  'upcoming' | 'earlySignupOpen' | 'signupOpen' | 'signupClosed' | 'finished'

/** An event as its cards show it; `startsAt` and `endsAt` are ISO days or instants. */
export interface EventSummary {
  readonly id: string
  readonly title: string
  readonly subtitle: string
  readonly startsAt: string
  readonly endsAt: string
  readonly status: EventStatusKind
  readonly thumbnailId: string
  readonly categories: readonly EventCategoryTag[]
}

/** Past events list filters; `year` is required, empty `search`/`categoryId` mean no filter. */
export interface PastEventFilters {
  readonly year: string
  readonly search: string
  readonly categoryId: string
}

/** Terms document linked to an event, as listed on the public event detail (no content). */
export interface EventTermsSummary {
  readonly id: string
  readonly name: string
  readonly required: boolean
  readonly displayOrder: number
}

/**
 * One event terms document from the signed-in user's perspective: its content plus whether (and
 * when) they already decided on it. `accepted` is `null` while undecided.
 */
export interface EventTermsDocumentState {
  readonly id: string
  readonly name: string
  readonly description: string
  readonly required: boolean
  readonly displayOrder: number
  readonly accepted: boolean | null
  readonly decidedAt: string | null
}

/**
 * The full terms state of an event for the signed-in user. `signupBlocked` is `true` while any
 * required document is not yet accepted.
 */
export interface EventTermsState {
  readonly documents: readonly EventTermsDocumentState[]
  readonly signupBlocked: boolean
}

/** Confirmed attendee with an account of their own, whom the leader contacts directly. */
export interface LeaderRosterUser {
  readonly firstName: string
  readonly lastName: string
  readonly email: string
  readonly phone: string
  readonly signedUpAt: string
}

/** Guardian of a dependent attendee: the leader's contact for that minor. */
interface LeaderRosterGuardian {
  readonly firstName: string
  readonly lastName: string
  readonly email: string
  readonly phone: string
}

/**
 * Confirmed minor signed up by their guardian. The API sends the age instead of the birth date;
 * `age` is `null` when no birth date is stored.
 */
export interface LeaderRosterDependent {
  readonly firstName: string
  readonly lastName: string
  readonly age: number | null
  readonly guardian: LeaderRosterGuardian
  readonly signedUpAt: string
}

/** Confirmed attendees of one role, split by kind because each kind carries different data. */
interface LeaderRosterRole {
  readonly id: string
  readonly name: string
  readonly users: readonly LeaderRosterUser[]
  readonly dependents: readonly LeaderRosterDependent[]
}

/**
 * Activity the signed-in user leads with a confirmed assignment and that has not ended, with its
 * confirmed attendees grouped by role (leaders, volunteers, participants).
 */
export interface LeaderRosterActivity {
  readonly id: string
  readonly title: string
  readonly location: string
  readonly startsAt: string
  readonly endsAt: string
  readonly roles: readonly LeaderRosterRole[]
}

/** A whole event as its public page shows it; every date is an ISO day or instant. */
export interface EventDetail {
  readonly id: string
  readonly title: string
  readonly subtitle: string
  readonly description: string
  readonly startsAt: string
  readonly endsAt: string
  readonly signupStartsAt: string
  readonly signupEndsAt: string
  /** When early signup opens for eligible users; `null` when the event has none. */
  readonly earlySignupStartsAt: string | null
  readonly status: EventStatusKind
  readonly thumbnailId: string
  readonly categories: readonly EventCategoryTag[]
  readonly terms: readonly EventTermsSummary[]
}

/** Home page event board: the featured event and the remaining upcoming ones. */
export interface HomeEvents {
  readonly featured: EventSummary | null
  readonly items: readonly EventSummary[]
}

/** An event as the admin events table lists it. */
export interface EventListing extends EventSummary {
  readonly featured: boolean
  readonly signupStartsAt: string
  readonly signupEndsAt: string
  readonly earlySignupStartsAt: string | null
}

/** A terms document an event links to and whether deciding on it is required. */
interface EventTermsInput {
  readonly documentId: string
  readonly required: boolean
}

/**
 * Values an event is created or replaced with: ISO days for the event itself, ISO instants for
 * its signup window, and terms documents in the order the event shows them.
 */
export interface EventInput {
  readonly title: string
  readonly subtitle: string
  readonly description: string
  readonly startsAt: string
  readonly endsAt: string
  readonly earlySignupStartsAt: string | null
  readonly signupStartsAt: string
  readonly signupEndsAt: string
  readonly thumbnailId: string
  readonly categoryIds: readonly string[]
  readonly terms: readonly EventTermsInput[]
}

/** Filters, sort and page of the admin events table. */
export interface EventListParams {
  /** Text matched against the title or the subtitle. */
  readonly search?: string
  readonly categoryTypeId?: string
  readonly eventDateFrom?: string
  readonly eventDateTo?: string
  readonly signupFrom?: string
  readonly signupTo?: string
  readonly page?: number
  readonly pageSize?: number
  readonly sort?: string
}
