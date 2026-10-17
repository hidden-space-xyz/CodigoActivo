import type {
  DashboardAnalyticsResponse,
  DashboardBreakdownResponse,
  DashboardSliceResponse,
  DashboardTimeSeriesResponse,
} from '@/shared/api/generated/models'

import type { Breakdown, DashboardAnalytics, Slice, TimeSeries } from '../model/types'

function toTimeSeries(series: DashboardTimeSeriesResponse): TimeSeries {
  return {
    buckets: series.buckets,
    series: series.series.map((set) => ({ key: set.key, values: set.values })),
  }
}

function toBreakdown(breakdown: DashboardBreakdownResponse): Breakdown {
  return {
    groups: breakdown.groups,
    series: breakdown.series.map((set) => ({ key: set.key, values: set.values })),
  }
}

function toSlice(slice: DashboardSliceResponse): Slice {
  return {
    key: slice.key,
    label: slice.label ?? null,
    color: slice.color ?? null,
    count: slice.count,
  }
}

/** Maps the dashboard analytics of a date range. */
export function toDashboardAnalytics(analytics: DashboardAnalyticsResponse): DashboardAnalytics {
  return {
    granularity: analytics.granularity,
    kpis: analytics.kpis.map((kpi) => ({
      key: kpi.key,
      total: kpi.total,
      inRange: kpi.inRange,
      previousRange: kpi.previousRange,
    })),
    newUsers: toTimeSeries(analytics.newUsers),
    inscriptions: toTimeSeries(analytics.inscriptions),
    topEvents: analytics.topEvents.map((event) => ({
      id: event.eventId,
      title: event.title,
      confirmed: event.confirmed,
      seatsFilled: event.seatsFilled,
      seatsDesired: event.seatsDesired,
    })),
    pendingSignups: {
      total: analytics.pendingSignups.total,
      events: analytics.pendingSignups.events.map((event) => ({
        id: event.eventId,
        title: event.title,
        pending: event.pending,
        oldestRequestedAt: event.oldestRequestedAt,
        nextActivityStartsAt: event.nextActivityStartsAt,
      })),
    },
    roleCoverage: analytics.roleCoverage.map((role) => ({
      key: role.key,
      confirmed: role.confirmed,
      desired: role.desired,
    })),
    participantProfile: toBreakdown(analytics.participantProfile),
    categoryDemand: analytics.categoryDemand.map(toSlice),
    recurrence: analytics.recurrence.map(toSlice),
    satisfaction: {
      average: analytics.satisfaction.average ?? null,
      scored: analytics.satisfaction.scored,
      events: analytics.satisfaction.events.map((event) => ({
        id: event.eventId,
        title: event.title,
        average: event.average,
        scored: event.scored,
      })),
    },
    occupancy: {
      confirmed: analytics.occupancy.confirmed,
      desired: analytics.occupancy.desired,
      events: analytics.occupancy.events.map((event) => ({
        id: event.eventId,
        title: event.title,
        confirmed: event.confirmed,
        desired: event.desired,
        activities: event.activities.map((activity) => ({
          id: activity.activityId,
          title: activity.title,
          startsAt: activity.startsAt,
          confirmed: activity.confirmed,
          desired: activity.desired,
        })),
      })),
    },
  }
}
