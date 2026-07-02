import type { MaybeRefOrGetter } from 'vue'
import { computed, toValue } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { fetchODataFunction, odataGuid } from '@/shared/api'
import type { EventSummaryResponse } from '@/shared/api'

export function useEventSummary(eventId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => ['reports', 'event-summary', toValue(eventId)] as const),
    queryFn: () =>
      fetchODataFunction<EventSummaryResponse>('EventSummary', {
        eventId: odataGuid(toValue(eventId)),
      }),
  })
}
