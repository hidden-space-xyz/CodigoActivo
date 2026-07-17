import { computed, type MaybeRefOrGetter, toValue } from 'vue'
import { keepPreviousData, useQuery } from '@tanstack/vue-query'

import { getApiReportsDashboardAnalytics } from '@/shared/api/generated/endpoints/reports/reports'

export function useDashboardAnalytics(range: MaybeRefOrGetter<{ from: string; to: string }>) {
  const params = computed(() => {
    const { from, to } = toValue(range)
    return { from, to }
  })

  return useQuery({
    queryKey: computed(
      () => ['dashboard', 'analytics', params.value.from, params.value.to] as const,
    ),
    queryFn: () => getApiReportsDashboardAnalytics(params.value).then((response) => response.data),
    placeholderData: keepPreviousData,
  })
}
