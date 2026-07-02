import { useQuery } from '@tanstack/vue-query'

import { fetchODataFunction } from '@/shared/api'
import type { DashboardSummaryResponse } from '@/shared/api'

export function useDashboardSummary() {
  return useQuery({
    queryKey: ['dashboard'] as const,
    queryFn: () => fetchODataFunction<DashboardSummaryResponse>('DashboardSummary'),
  })
}
