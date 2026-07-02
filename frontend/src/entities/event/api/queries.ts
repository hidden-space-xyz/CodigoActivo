import { computed, ref, toValue, watch, type MaybeRefOrGetter } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { eventQueryKeys } from './query-keys'
import {
  getEventByIdRequest,
  getHomeEventsRequest,
  getPastEventsRequest,
  getPastEventYearsRequest,
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
  const yearsQuery = useQuery({
    queryKey: eventQueryKeys.pastYears(),
    queryFn: () => getPastEventYearsRequest(),
  })

  const years = computed(() => yearsQuery.data.value ?? [])
  const selectedYear = ref<string>('')

  watch(
    years,
    (list) => {
      const [first] = list
      if (first && !selectedYear.value) selectedYear.value = first
    },
    { immediate: true },
  )

  const eventsQuery = useQuery({
    queryKey: computed(() => eventQueryKeys.past(selectedYear.value)),
    queryFn: () => getPastEventsRequest(selectedYear.value),
    enabled: computed(() => Boolean(selectedYear.value)),
  })

  function setYear(year: string): void {
    selectedYear.value = year
  }

  return {
    years,
    selectedYear,
    setYear,
    pastEvents: computed(() => eventsQuery.data.value ?? []),
    isLoading: computed(() => yearsQuery.isLoading.value || eventsQuery.isLoading.value),
    isError: computed(() => yearsQuery.isError.value || eventsQuery.isError.value),
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
