import type { MaybeRefOrGetter } from 'vue'
import { computed, toValue } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import {
  getApiReportsEventsEventIdAttendees,
  getApiReportsEventsEventIdBadges,
  getApiReportsEventsEventIdRoster,
  getApiReportsEventsEventIdSummary,
} from '@/shared/api/generated/endpoints/reports/reports'

export function useEventSummary(eventId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => ['reports', 'event-summary', toValue(eventId)] as const),
    queryFn: () => getApiReportsEventsEventIdSummary(toValue(eventId)).then((r) => r.data),
  })
}

export function useEventAttendees(
  eventId: MaybeRefOrGetter<string>,
  enabled: MaybeRefOrGetter<boolean> = true,
) {
  return useQuery({
    queryKey: computed(() => ['reports', 'event-attendees', toValue(eventId)] as const),
    queryFn: () => getApiReportsEventsEventIdAttendees(toValue(eventId)).then((r) => r.data),
    enabled: computed(() => toValue(enabled)),
  })
}

export function useEventBadges(eventId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => ['reports', 'event-badges', toValue(eventId)] as const),
    queryFn: () => getApiReportsEventsEventIdBadges(toValue(eventId)).then((r) => r.data),
    // The sheet is a print deliverable: assignment mutations don't invalidate this key,
    // so always refetch on open to never print a list cached before the latest changes.
    staleTime: 0,
    refetchOnMount: 'always',
  })
}

export function useEventRoster(eventId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => ['reports', 'event-roster', toValue(eventId)] as const),
    queryFn: () => getApiReportsEventsEventIdRoster(toValue(eventId)).then((r) => r.data),
    staleTime: 0,
    refetchOnMount: 'always',
  })
}
