export interface EventAgendaItem {
  readonly time: string
  readonly description: string
}

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
