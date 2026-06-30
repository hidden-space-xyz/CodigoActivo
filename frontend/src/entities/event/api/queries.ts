import { computed, ref, toValue, type MaybeRefOrGetter } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { ALL_YEARS, availableYears, selectEventsByYear } from '../lib/filter-events'
import { eventQueryKeys } from './query-keys'
import {
  getEventByIdRequest,
  getHomeEventsRequest,
  getPastEventsRequest,
  getUpcomingEventsRequest,
} from './requests'

export function useUpcomingEvents() {
  const query = useQuery({
    queryKey: eventQueryKeys.upcoming(),
    queryFn: () => getUpcomingEventsRequest(),
  })

  return {
    upcomingEvents: query.data,
    isLoading: query.isLoading,
    isError: query.isError,
  }
}

export function useHomeEvents() {
  const query = useQuery({
    queryKey: eventQueryKeys.board(),
    queryFn: () => getHomeEventsRequest(),
  })

  return {
    featured: computed(() => query.data.value?.featured ?? null),
    items: computed(() => query.data.value?.items ?? []),
    isLoading: query.isLoading,
    isError: query.isError,
  }
}

export function usePastEvents() {
  const query = useQuery({
    queryKey: eventQueryKeys.past(),
    queryFn: () => getPastEventsRequest(),
  })

  const selectedYear = ref<string>(ALL_YEARS)

  const years = computed(() => availableYears(query.data.value ?? []))
  const filteredEvents = computed(() =>
    selectEventsByYear(query.data.value ?? [], selectedYear.value),
  )

  function setYear(year: string): void {
    selectedYear.value = year
  }

  return {
    isLoading: query.isLoading,
    isError: query.isError,
    years,
    filteredEvents,
    selectedYear,
    setYear,
    ALL_YEARS,
  }
}

export function useEventDetail(eventId: MaybeRefOrGetter<string>) {
  const id = computed(() => toValue(eventId))

  const query = useQuery({
    queryKey: computed(() => eventQueryKeys.detail(id.value)),
    queryFn: () => getEventByIdRequest(id.value),
  })

  const notFound = computed(() => !query.isLoading.value && query.data.value === null)

  return {
    event: query.data,
    isLoading: query.isLoading,
    isError: query.isError,
    notFound,
  }
}
