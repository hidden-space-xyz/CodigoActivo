export interface EventCategoryTag {
  readonly id: string
  readonly name: string
  readonly color: string
}

export interface UpcomingEvent {
  readonly id: string
  readonly title: string
  readonly slogan: string
  readonly date: string
  readonly status: string
  readonly thumbnailId: string
  readonly categories: readonly EventCategoryTag[]
}

export interface PastEvent {
  readonly id: string
  readonly title: string
  readonly eventName: string
  readonly year: string
  readonly thumbnailId: string
  readonly categories: readonly EventCategoryTag[]
}

export interface EventDetail {
  readonly id: string
  readonly title: string
  readonly subtitle: string
  readonly description: string
  readonly dateLabel: string
  readonly signupLabel: string
  readonly status: string
  readonly thumbnailId: string
  readonly signupOpen: boolean
  readonly categories: readonly EventCategoryTag[]
}

export interface HomeEvents {
  readonly featured: UpcomingEvent | null
  readonly items: readonly UpcomingEvent[]
}
