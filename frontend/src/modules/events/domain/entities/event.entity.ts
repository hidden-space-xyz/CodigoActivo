export interface UpcomingEvent {
  readonly id: string
  readonly title: string
  readonly slogan: string
  readonly date: string
  readonly dayLabel: string
  readonly monthLabel: string
  readonly status: string
  readonly description: string
  readonly featured: boolean
}

export interface PastEvent {
  readonly id: string
  readonly title: string
  readonly eventName: string
  readonly year: string
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
}
