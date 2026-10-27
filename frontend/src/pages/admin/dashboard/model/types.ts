/** A counter tile: all-time total, count in the range and count in the range just before it. */
export interface DashboardKpi {
  readonly key: string
  readonly total: number
  readonly inRange: number
  readonly previousRange: number
}

/** One line or bar group of a chart, with a value per bucket or group. */
interface Series {
  readonly key: string
  readonly values: readonly number[]
}

/** Values of several series over the same date buckets, each named by its first ISO day. */
export interface TimeSeries {
  readonly buckets: readonly string[]
  readonly series: readonly Series[]
}

/** Values of several series over the same groups, such as people per age group and gender. */
export interface Breakdown {
  readonly groups: readonly string[]
  readonly series: readonly Series[]
}

/** Counted item of a chart; `label` and `color` come from the API for catalog-driven items. */
export interface Slice {
  readonly key: string
  readonly label: string | null
  readonly color: string | null
  readonly count: number
}

/**
 * Event of the ranking of confirmed signups in the activities of the range, with the seats those
 * activities filled (each role up to its desired number) and wanted.
 */
export interface TopEvent {
  readonly id: string
  readonly title: string
  readonly confirmed: number
  readonly seatsFilled: number
  readonly seatsDesired: number
}

/** Event with signups still waiting for a decision in its upcoming activities. */
interface PendingEvent {
  readonly id: string
  readonly title: string
  readonly pending: number
  readonly oldestRequestedAt: string
  readonly nextActivityStartsAt: string
}

/** Signups waiting for a decision in upcoming activities, overall and per event. */
export interface PendingSignups {
  readonly total: number
  readonly events: readonly PendingEvent[]
}

/** Seats of one role filled by confirmed people versus wanted across the upcoming activities. */
export interface RoleCoverage {
  readonly key: string
  readonly confirmed: number
  readonly desired: number
}

/** Mean score of one event that ended in the range. */
interface EventSatisfaction {
  readonly id: string
  readonly title: string
  readonly average: number
  readonly scored: number
}

/** Scores of the events that ended in the range; `average` is `null` without scored ratings. */
export interface Satisfaction {
  readonly average: number | null
  readonly scored: number
  readonly events: readonly EventSatisfaction[]
}

/** Places filled by confirmed people versus desired places of one activity, capped per role. */
interface OccupancyActivity {
  readonly id: string
  readonly title: string
  readonly startsAt: string
  readonly confirmed: number
  readonly desired: number
}

/** Filled versus desired places of one event, with its activities. */
interface OccupancyEvent {
  readonly id: string
  readonly title: string
  readonly confirmed: number
  readonly desired: number
  readonly activities: readonly OccupancyActivity[]
}

/** Filled versus desired places, overall and per event. */
export interface Occupancy {
  readonly confirmed: number
  readonly desired: number
  readonly events: readonly OccupancyEvent[]
}

/**
 * Figures of the admin dashboard for a date range; `granularity` (`day`, `week` or `month`, by the
 * length of the range) sizes the time buckets. The pending signups, role coverage and occupancy
 * describe upcoming activities whatever the range.
 */
export interface DashboardAnalytics {
  readonly granularity: string
  readonly kpis: readonly DashboardKpi[]
  readonly newUsers: TimeSeries
  readonly inscriptions: TimeSeries
  readonly topEvents: readonly TopEvent[]
  readonly pendingSignups: PendingSignups
  readonly roleCoverage: readonly RoleCoverage[]
  readonly participantProfile: Breakdown
  readonly categoryDemand: readonly Slice[]
  readonly recurrence: readonly Slice[]
  readonly satisfaction: Satisfaction
  readonly occupancy: Occupancy
}

/** Inclusive range of ISO days the dashboard covers. */
export interface DashboardRange {
  readonly from: string
  readonly to: string
}
