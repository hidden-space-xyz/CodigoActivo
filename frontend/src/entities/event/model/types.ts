export interface EventCategoryTag {
  readonly id: string
  readonly name: string
  readonly color: string
}

export type EventStatusKind = 'upcoming' | 'signupOpen' | 'signupClosed' | 'finished'

export interface EventStatus {
  readonly kind: EventStatusKind
  readonly label: string
}

export interface UpcomingEvent {
  readonly id: string
  readonly title: string
  readonly slogan: string
  readonly date: string
  readonly status: EventStatus
  readonly thumbnailId: string
  readonly categories: readonly EventCategoryTag[]
}

export interface PastEvent {
  readonly id: string
  readonly title: string
  readonly eventName: string
  readonly date: string
  readonly status: EventStatus
  readonly thumbnailId: string
  readonly categories: readonly EventCategoryTag[]
}

export interface EventDetail {
  readonly id: string
  readonly title: string
  readonly subtitle: string
  readonly description: string
  readonly startsAt: string | null
  readonly endsAt: string | null
  readonly dateLabel: string
  readonly signupLabel: string
  readonly status: EventStatus
  readonly thumbnailId: string
  readonly signupOpen: boolean
  readonly categories: readonly EventCategoryTag[]
}

export interface HomeEvents {
  readonly featured: UpcomingEvent | null
  readonly items: readonly UpcomingEvent[]
}
