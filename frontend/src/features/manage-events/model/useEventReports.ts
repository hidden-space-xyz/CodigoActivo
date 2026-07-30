import type { MaybeRefOrGetter } from 'vue'
import { computed, ref, toValue } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { getApiEventsEventIdRatings } from '@/shared/api/generated/endpoints/events/events'
import {
  getApiReportsEventsEventIdAttendees,
  getApiReportsEventsEventIdBadges,
  getApiReportsEventsEventIdRoster,
  getApiReportsEventsEventIdSummary,
} from '@/shared/api/generated/endpoints/reports/reports'
import type {
  EventAttendeeResponse,
  EventRatingListItemResponse,
  Gender,
  GetApiEventsEventIdRatingsParams,
  GetApiReportsEventsEventIdAttendeesParams,
} from '@/shared/api/generated/models'
import { toPage } from '@/shared/api'
import { useServerTable } from '@/shared/lib'
import { eventQueryKeys, eventReportQueryKeys } from '@/entities/event'

const ATTENDEE_EXPORT_PAGE_SIZE = 100
const ATTENDEE_EXPORT_PAGE_LIMIT = 200

export function useEventSummary(eventId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => eventReportQueryKeys.summary(toValue(eventId))),
    queryFn: () => getApiReportsEventsEventIdSummary(toValue(eventId)).then((r) => r.data),
  })
}

export function useEventAttendeesTable(
  eventId: MaybeRefOrGetter<string>,
  active: MaybeRefOrGetter<boolean>,
) {
  const search = ref('')
  const userTypeId = ref<string | null>(null)
  const gender = ref<Gender | null>(null)
  const activityId = ref<string | null>(null)
  const roleTypeId = ref<string | null>(null)
  const statusId = ref<string | null>(null)

  const filterParams = (): Record<string, unknown> => ({
    search: search.value.trim() || undefined,
    userTypeId: userTypeId.value ?? undefined,
    gender: gender.value ?? undefined,
    activityId: activityId.value ?? undefined,
    roleTypeId: roleTypeId.value ?? undefined,
    statusId: statusId.value ?? undefined,
  })

  const table = useServerTable<EventAttendeeResponse, GetApiReportsEventsEventIdAttendeesParams>({
    queryKey: eventReportQueryKeys.attendees(),
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

  return {
    table,
    search,
    userTypeId,
    gender,
    activityId,
    roleTypeId,
    statusId,
    filterParams,
    fetchAllAttendees,
  }
}

export function useEventRatingsTable(
  eventId: MaybeRefOrGetter<string>,
  active: MaybeRefOrGetter<boolean>,
) {
  const table = useServerTable<EventRatingListItemResponse, GetApiEventsEventIdRatingsParams>({
    queryKey: eventQueryKeys.ratings(),
    fetchPage: (params) => getApiEventsEventIdRatings(toValue(eventId), params).then(toPage),
    defaultSort: { field: 'createdAt', order: -1 },
    extraParams: () => ({ eventId: toValue(eventId) }),
    enabled: () => toValue(active),
  })

  return { table }
}

export function useEventBadges(eventId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => eventReportQueryKeys.badges(toValue(eventId))),
    queryFn: () => getApiReportsEventsEventIdBadges(toValue(eventId)).then((r) => r.data),
    staleTime: 0,
    refetchOnMount: 'always',
  })
}

export function useEventRoster(eventId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => eventReportQueryKeys.roster(toValue(eventId))),
    queryFn: () => getApiReportsEventsEventIdRoster(toValue(eventId)).then((r) => r.data),
    staleTime: 0,
    refetchOnMount: 'always',
  })
}
