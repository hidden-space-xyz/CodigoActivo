import { useQuery } from '@tanstack/vue-query'

import { getApiReportsDashboard } from '@/shared/api/generated/endpoints/reports/reports'

export function useDashboardSummary() {
  return useQuery({
    queryKey: ['dashboard'] as const,
    queryFn: () => getApiReportsDashboard().then((r) => r.data),
  })
}
