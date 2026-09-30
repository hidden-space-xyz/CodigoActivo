import type {
  DashboardAnalyticsResponse,
  DashboardSliceResponse,
  DashboardTimeSeriesResponse,
} from '@/shared/api/generated/models'

import type { DashboardAnalytics, Slice, TimeSeries } from '../model/types'

function toTimeSeries(series: DashboardTimeSeriesResponse): TimeSeries {
  return {
    buckets: series.buckets,
    series: series.series.map((set) => ({ key: set.key, values: set.values })),
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
    userGrowth: toTimeSeries(analytics.userGrowth),
    inscriptions: toTimeSeries(analytics.inscriptions),
    contentPublished: toTimeSeries(analytics.contentPublished),
    eventsCalendar: toTimeSeries(analytics.eventsCalendar),
    usersByType: analytics.usersByType.map(toSlice),
    audienceComposition: analytics.audienceComposition.map(toSlice),
    participantsByGender: analytics.participantsByGender.map(toSlice),
    eventsByCategory: analytics.eventsByCategory.map(toSlice),
    topEvents: analytics.topEvents.map((event) => ({
      id: event.eventId,
      title: event.title,
      confirmed: event.confirmed,
    })),
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
