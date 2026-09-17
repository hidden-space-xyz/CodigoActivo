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

/** Terms document attendees must accept before signing up to the event activities. */
export interface EventTermsInfo {
  readonly id: string
  readonly name: string
  readonly description: string
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
  readonly terms: EventTermsInfo | null
}

/** Home page event board: the featured event and the remaining upcoming ones. */
export interface HomeEvents {
  readonly featured: UpcomingEvent | null
  readonly items: readonly UpcomingEvent[]
}
