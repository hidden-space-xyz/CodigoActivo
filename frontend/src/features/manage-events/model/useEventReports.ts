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

const ATTENDEE_EXPORT_PAGE_SIZE = 100
const ATTENDEE_EXPORT_PAGE_LIMIT = 200

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

  const filterParams = (): Record<string, unknown> => ({
    search: search.value.trim() || undefined,
    userTypeId: userTypeId.value ?? undefined,
    activityId: activityId.value ?? undefined,
    roleTypeId: roleTypeId.value ?? undefined,
    statusId: statusId.value ?? undefined,
  })

  const table = useServerTable<EventAttendeeResponse, GetApiReportsEventsEventIdAttendeesParams>({
    queryKey: ['reports', 'event-attendees'],
    fetchPage: (params) =>
      getApiReportsEventsEventIdAttendees(toValue(eventId), params).then(toPage),
    defaultSort: { field: 'firstName', order: 1 },
    extraParams: () => ({ eventId: toValue(eventId), ...filterParams() }),
    enabled: () => toValue(active),
  })

  async function fetchAllAttendees(): Promise<EventAttendeeResponse[]> {
    const sort = table.sortField.value
      ? `${table.sortOrder.value === -1 ? '-' : ''}${table.sortField.value}`
      : undefined
    const filters = filterParams()
    const attendees: EventAttendeeResponse[] = []

    for (let page = 1; page <= ATTENDEE_EXPORT_PAGE_LIMIT; page += 1) {
      const params = {
        ...filters,
        sort,
        page,
        pageSize: ATTENDEE_EXPORT_PAGE_SIZE,
      } as GetApiReportsEventsEventIdAttendeesParams
      const { items, total } = await getApiReportsEventsEventIdAttendees(
        toValue(eventId),
        params,
      ).then(toPage)
      attendees.push(...items)
      if (items.length === 0 || attendees.length >= total) break
    }

    return attendees
  }

  return { table, search, userTypeId, activityId, roleTypeId, statusId, fetchAllAttendees }
}

export function useEventBadges(eventId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => ['reports', 'event-badges', toValue(eventId)] as const),
    queryFn: () => getApiReportsEventsEventIdBadges(toValue(eventId)).then((r) => r.data),
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
