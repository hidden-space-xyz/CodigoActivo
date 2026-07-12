import type { MaybeRefOrGetter } from 'vue'
import { computed, ref, toValue } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import {
  getApiReportsEventsEventIdAttendees,
  getApiReportsEventsEventIdBadges,
  getApiReportsEventsEventIdRoster,
  getApiReportsEventsEventIdSummary,
} from '@/shared/api/generated/endpoints/reports/reports'
import type {
  EventAttendeeResponse,
  GetApiReportsEventsEventIdAttendeesParams,
} from '@/shared/api/generated/models'
import { toPage } from '@/shared/api'
import { useServerTable } from '@/shared/lib'

export function useEventSummary(eventId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => ['reports', 'event-summary', toValue(eventId)] as const),
    queryFn: () => getApiReportsEventsEventIdSummary(toValue(eventId)).then((r) => r.data),
  })
}

export function useEventAttendeesTable(
  eventId: MaybeRefOrGetter<string>,
  active: MaybeRefOrGetter<boolean>,
) {
  const search = ref('')
  const userTypeId = ref<string | null>(null)
  const activityId = ref<string | null>(null)
  const roleTypeId = ref<string | null>(null)
  const statusId = ref<string | null>(null)

  const table = useServerTable<EventAttendeeResponse, GetApiReportsEventsEventIdAttendeesParams>({
    queryKey: ['reports', 'event-attendees'],
    fetchPage: (params) =>
      getApiReportsEventsEventIdAttendees(toValue(eventId), params).then(toPage),
    defaultSort: { field: 'firstName', order: 1 },
    extraParams: () => ({
      eventId: toValue(eventId),
      search: search.value.trim() || undefined,
      userTypeId: userTypeId.value ?? undefined,
      activityId: activityId.value ?? undefined,
      roleTypeId: roleTypeId.value ?? undefined,
      statusId: statusId.value ?? undefined,
    }),
    enabled: () => toValue(active),
  })

  return { table, search, userTypeId, activityId, roleTypeId, statusId }
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
