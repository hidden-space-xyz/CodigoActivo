import { computed, toValue, type MaybeRefOrGetter } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { usePagedList } from '@/shared/lib'

import { eventQueryKeys } from './query-keys'
import {
  getEventByIdRequest,
  getHomeEventsRequest,
  getPastEventsPageRequest,
  getPastEventYearsRequest,
  getUpcomingEventsPageRequest,
} from './requests'

export function useUpcomingEventsPaged() {
  return usePagedList({
    queryKey: () => eventQueryKeys.upcoming(),
    fetchPage: (page, pageSize) => getUpcomingEventsPageRequest(page, pageSize),
  })
}

export function usePastEventYears() {
  const query = useQuery({
    queryKey: eventQueryKeys.pastYears(),
    queryFn: () => getPastEventYearsRequest(),
  })

  return {
    years: computed(() => query.data.value ?? []),
    isLoading: query.isLoading,
    isError: query.isError,
  }
}

export function usePastEventsPaged(year: MaybeRefOrGetter<string>) {
  const selectedYear = computed(() => toValue(year))

  return usePagedList({
    queryKey: () => eventQueryKeys.past(selectedYear.value),
    fetchPage: (page, pageSize) => getPastEventsPageRequest(selectedYear.value, page, pageSize),
    enabled: () => selectedYear.value !== '',
  })
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
