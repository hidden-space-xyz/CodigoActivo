import { computed, type MaybeRefOrGetter, toValue } from 'vue'
import { keepPreviousData, useQuery } from '@tanstack/vue-query'

import { eventReportQueryKeys, getDashboardAnalyticsRequest } from '@/entities/event'

export function useDashboardAnalytics(range: MaybeRefOrGetter<{ from: string; to: string }>) {
  const params = computed(() => {
    const { from, to } = toValue(range)
    return { from, to }
  })

  return useQuery({
    queryKey: computed(() =>
      eventReportQueryKeys.dashboardAnalytics(params.value.from, params.value.to),
    ),
    queryFn: () => getDashboardAnalyticsRequest(params.value),
    placeholderData: keepPreviousData,
  })
}
