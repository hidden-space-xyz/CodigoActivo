import { keepPreviousData, queryOptions } from '@tanstack/vue-query'

import type { DashboardRange } from '../model/types'
import { getDashboardAnalyticsRequest } from './requests'

/** Query keys of the dashboard, under the root of every report. */
export const dashboardKeys = {
  all: ['reports'] as const,
  analytics: (range: DashboardRange) =>
    [...dashboardKeys.all, 'dashboard-analytics', range.from, range.to] as const,
}

/** Query options of the dashboard. */
export const dashboardQueries = {
  /** Figures of a range of days; the previous range stays shown while a new one loads. */
  analytics: (range: DashboardRange) =>
    queryOptions({
      queryKey: dashboardKeys.analytics(range),
      queryFn: () => getDashboardAnalyticsRequest(range),
      placeholderData: keepPreviousData,
    }),
}
