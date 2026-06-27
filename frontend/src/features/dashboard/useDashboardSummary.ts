import { useQuery } from '@tanstack/vue-query'

import { getApiReportsDashboardSummaryCounters } from '@/shared/api/generated/endpoints/reports/reports'
import { queryKeys } from '@/shared/api/query-keys'

export function useDashboardSummary() {
  return useQuery({
    queryKey: queryKeys.dashboard,
    queryFn: ({ signal }) =>
      getApiReportsDashboardSummaryCounters({ signal }).then((response) => response.data),
  })
}
