import type { MaybeRefOrGetter } from 'vue'
import { computed, ref, toValue } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import type {
  EventAttendeeResponse,
  EventRatingListItemResponse,
  Gender,
  GetApiEventsEventIdRatingsParams,
  GetApiReportsEventsEventIdAttendeesParams,
} from '@/shared/api/generated/models'
import { fetchAllPages, useServerTable } from '@/shared/lib'
import {
  eventQueryKeys,
  eventReportQueryKeys,
  getEventAttendeesPageRequest,
  getEventBadgesRequest,
  getEventRatingsPageRequest,
  getEventRosterRequest,
  getEventSummaryRequest,
} from '@/entities/event'

export function useEventSummary(eventId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => eventReportQueryKeys.summary(toValue(eventId))),
    queryFn: () => getEventSummaryRequest(toValue(eventId)),
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
    fetchPage: (params) => getEventAttendeesPageRequest(toValue(eventId), params),
    defaultSort: { field: 'firstName', order: 1 },
    extraParams: () => ({ eventId: toValue(eventId), ...filterParams() }),
    enabled: () => toValue(active),
  })

  function fetchAllAttendees(): Promise<EventAttendeeResponse[]> {
    return fetchAllPages(
      (params) =>
        getEventAttendeesPageRequest(
          toValue(eventId),
          params as GetApiReportsEventsEventIdAttendeesParams,
        ),
      { ...filterParams(), sort: table.sortParam.value },
    )
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
    fetchPage: (params) => getEventRatingsPageRequest(toValue(eventId), params),
    defaultSort: { field: 'createdAt', order: -1 },
    extraParams: () => ({ eventId: toValue(eventId) }),
    enabled: () => toValue(active),
  })

  return { table }
}

export function useEventBadges(eventId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => eventReportQueryKeys.badges(toValue(eventId))),
    queryFn: () => getEventBadgesRequest(toValue(eventId)),
    staleTime: 0,
    refetchOnMount: 'always',
  })
}

export function useEventRoster(eventId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => eventReportQueryKeys.roster(toValue(eventId))),
    queryFn: () => getEventRosterRequest(toValue(eventId)),
    staleTime: 0,
    refetchOnMount: 'always',
  })
}
