import { getApiReportsDashboardAnalytics } from '@/shared/api/generated/endpoints/reports/reports'

import type { DashboardAnalytics, DashboardRange } from '../model/types'
import { toDashboardAnalytics } from './mapper'

/** Loads the dashboard figures of a range of days. */
export async function getDashboardAnalyticsRequest(
  range: DashboardRange,
): Promise<DashboardAnalytics> {
  const { data } = await getApiReportsDashboardAnalytics({ from: range.from, to: range.to })
  return toDashboardAnalytics(data)
}
