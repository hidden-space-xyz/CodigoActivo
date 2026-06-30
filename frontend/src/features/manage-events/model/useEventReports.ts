import type { MaybeRefOrGetter } from 'vue'
import { computed, toValue } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { getApiReportsEventEventIdSummary } from '@/shared/api/generated/endpoints/reports/reports'

export function useEventSummary(eventId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => ['reports', 'event-summary', toValue(eventId)] as const),
    queryFn: ({ signal }) =>
      getApiReportsEventEventIdSummary(toValue(eventId), { signal }).then((r) => r.data),
  })
}
