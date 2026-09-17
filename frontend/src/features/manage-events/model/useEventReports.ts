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

/** Report summary for an event; invalidated by activity and assignment changes. */
export function useEventSummary(eventId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => eventReportQueryKeys.summary(toValue(eventId))),
    queryFn: () => getEventSummaryRequest(toValue(eventId)),
  })
}

/**
 * Server-side attendee table for an event with search, user type, gender, activity, role and status
 * filters. Fetches only while `active` is true (its tab is selected). `fetchAllAttendees` loads
 * every page with the current filters and sort (for exports).
 */
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

/** Server-side table of an event's participant ratings, newest first; fetches while `active`. */
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

/** Per-participant badge data for an event, consumed by the printable badges page. */
export function useEventBadges(eventId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => eventReportQueryKeys.badges(toValue(eventId))),
    queryFn: () => getEventBadgesRequest(toValue(eventId)),
  })
}

/** Event participants grouped by activity, consumed by the printable roster page. */
export function useEventRoster(eventId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => eventReportQueryKeys.roster(toValue(eventId))),
    queryFn: () => getEventRosterRequest(toValue(eventId)),
  })
}
