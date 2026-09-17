/** Colored category label shown on event cards; `color` is the CSS color set by admins. */
export interface EventCategoryTag {
  readonly id: string
  readonly name: string
  readonly color: string
}

/** Lifecycle stage derived client-side from the signup window and end date; drives card styling. */
export type EventStatusKind =
  'upcoming' | 'earlySignupOpen' | 'signupOpen' | 'signupClosed' | 'finished'

/** Status kind plus its translated label, resolved when the event is mapped. */
export interface EventStatus {
  readonly kind: EventStatusKind
  readonly label: string
}

/** Card model for an upcoming event, mapped from `EventListItemResponse`. */
export interface UpcomingEvent {
  readonly id: string
  readonly title: string
  readonly slogan: string
  readonly date: string
  readonly status: EventStatus
  readonly thumbnailId: string
  readonly categories: readonly EventCategoryTag[]
}

/** Card model for a finished event; `eventName` holds the event subtitle. */
export interface PastEvent {
  readonly id: string
  readonly title: string
  readonly eventName: string
  readonly date: string
  readonly status: EventStatus
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

/** Role type an activity assignment cell in the signup statistics belongs to. */
interface EventSignupStatsRole {
  readonly id: string
  readonly name: string
}

/** Assignment status a signup statistics cell belongs to. */
interface EventSignupStatsStatus {
  readonly id: string
  readonly name: string
}

/** One non-zero role/status count for an activity in the signup statistics. */
interface EventSignupStatsCell {
  readonly roleId: string
  readonly statusId: string
  readonly count: number
}

/** Signup statistics of one activity, with only the non-zero role/status cells from the API. */
export interface EventSignupStatsActivity {
  readonly id: string
  readonly title: string
  readonly startsAt: string | null
  readonly cells: readonly EventSignupStatsCell[]
}

/** Event-wide signup totals by status. */
interface EventSignupStatsTotals {
  readonly total: number
  readonly requested: number
  readonly confirmed: number
  readonly denied: number
}

/**
 * Signup statistics for every activity of an event, for the admin/member statistics panel. Cells
 * missing from `activities[].cells` count as zero; the client fills them in before charting.
 */
export interface EventSignupStats {
  readonly eventId: string
  readonly roles: readonly EventSignupStatsRole[]
  readonly statuses: readonly EventSignupStatsStatus[]
  readonly activities: readonly EventSignupStatsActivity[]
  readonly totals: EventSignupStatsTotals
}

/** Public detail page model, mapped from `EventResponse` with display labels pre-formatted. */
export interface EventDetail {
  readonly id: string
  readonly title: string
  readonly subtitle: string
  readonly description: string
  readonly startsAt: string | null
  readonly endsAt: string | null
  readonly dateLabel: string
  readonly signupLabel: string
  readonly earlySignupLabel: string | null
  readonly status: EventStatus
  readonly thumbnailId: string
  readonly signupOpen: boolean
  readonly earlySignupOpen: boolean
  readonly categories: readonly EventCategoryTag[]
  readonly terms: readonly EventTermsSummary[]
}

/** Home page event board: the featured event and the remaining upcoming ones. */
export interface HomeEvents {
  readonly featured: UpcomingEvent | null
  readonly items: readonly UpcomingEvent[]
}
