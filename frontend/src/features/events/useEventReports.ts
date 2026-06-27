import type { MaybeRefOrGetter } from 'vue'
import { computed, toValue } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import {
  getApiReportsEventEventIdAssignments,
  getApiReportsEventEventIdSummary,
} from '@/shared/api/generated/endpoints/reports/reports'
import { queryKeys } from '@/shared/api/query-keys'

export function useEventSummary(eventId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => queryKeys.eventSummary(toValue(eventId))),
    queryFn: ({ signal }) =>
      getApiReportsEventEventIdSummary(toValue(eventId), { signal }).then((r) => r.data),
  })
}

export function useEventAssignments(eventId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => queryKeys.eventAssignments(toValue(eventId))),
    queryFn: ({ signal }) =>
      getApiReportsEventEventIdAssignments(toValue(eventId), { signal }).then((r) => r.data),
  })
}
