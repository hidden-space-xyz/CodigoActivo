import { useQuery } from '@tanstack/vue-query'

import { getApiReportsDashboardSummaryCounters } from '@/shared/api/generated/endpoints/reports/reports'

export function useDashboardSummary() {
  return useQuery({
    queryKey: ['dashboard'] as const,
    queryFn: ({ signal }) =>
      getApiReportsDashboardSummaryCounters({ signal }).then((response) => response.data),
  })
}
