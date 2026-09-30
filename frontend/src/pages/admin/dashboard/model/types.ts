/** A counter tile: all-time total, count in the range and count in the range just before it. */
export interface DashboardKpi {
  readonly key: string
  readonly total: number
  readonly inRange: number
  readonly previousRange: number
}

/** One line or bar group of a time chart, with a value per bucket. */
interface Series {
  readonly key: string
  readonly values: readonly number[]
}

/** Values of several series over the same date buckets (ISO days). */
export interface TimeSeries {
  readonly buckets: readonly string[]
  readonly series: readonly Series[]
}

/** Slice of a doughnut; `label` and `color` come from the API for catalog-driven slices. */
export interface Slice {
  readonly key: string
  readonly label: string | null
  readonly color: string | null
  readonly count: number
}

/** Event of the ranking of most confirmed signups. */
interface TopEvent {
  readonly id: string
  readonly title: string
  readonly confirmed: number
}

/** Confirmed versus desired places of one activity. */
interface OccupancyActivity {
  readonly id: string
  readonly title: string
  readonly startsAt: string
  readonly confirmed: number
  readonly desired: number
}

/** Confirmed versus desired places of one event, with its activities. */
interface OccupancyEvent {
  readonly id: string
  readonly title: string
  readonly confirmed: number
  readonly desired: number
  readonly activities: readonly OccupancyActivity[]
}

/** Confirmed versus desired places, overall and per event. */
export interface Occupancy {
  readonly confirmed: number
  readonly desired: number
  readonly events: readonly OccupancyEvent[]
}

/** Figures of the admin dashboard for a date range; `granularity` sizes the time buckets. */
export interface DashboardAnalytics {
  readonly granularity: string
  readonly kpis: readonly DashboardKpi[]
  readonly userGrowth: TimeSeries
  readonly inscriptions: TimeSeries
  readonly contentPublished: TimeSeries
  readonly eventsCalendar: TimeSeries
  readonly usersByType: readonly Slice[]
  readonly audienceComposition: readonly Slice[]
  readonly participantsByGender: readonly Slice[]
  readonly eventsByCategory: readonly Slice[]
  readonly topEvents: readonly TopEvent[]
  readonly occupancy: Occupancy
}

/** Inclusive range of ISO days the dashboard covers. */
export interface DashboardRange {
  readonly from: string
  readonly to: string
}
