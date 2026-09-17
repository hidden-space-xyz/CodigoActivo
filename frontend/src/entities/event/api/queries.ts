import { computed, toValue, type MaybeRefOrGetter } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { usePagedList } from '@/shared/lib'

import type { PastEventFilters } from '../model/types'
import { eventQueryKeys } from './query-keys'
import {
  getEventByIdRequest,
  getHomeEventsRequest,
  getPastEventCategoriesRequest,
  getPastEventsPageRequest,
  getPastEventYearsRequest,
  getUpcomingEventsPageRequest,
} from './requests'

/** Infinite list of upcoming events ordered by start date, for the public events page. */
export function useUpcomingEventsPaged() {
  return usePagedList({
    queryKey: () => eventQueryKeys.upcoming(),
    fetchPage: (page, pageSize) => getUpcomingEventsPageRequest(page, pageSize),
  })
}

/** Years that have past events, used as filter options; `years` is empty until loaded. */
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

/** Categories used by past events, used as filter options; `categories` is empty until loaded. */
export function usePastEventCategories() {
  const query = useQuery({
    queryKey: eventQueryKeys.pastCategories(),
    queryFn: () => getPastEventCategoriesRequest(),
  })

  return {
    categories: computed(() => query.data.value ?? []),
    isLoading: query.isLoading,
    isError: query.isError,
  }
}

/**
 * Infinite list of past events for the selected filters, newest first. Empty `search` and
 * `categoryId` are not sent; nothing loads while `year` is empty.
 */
export function usePastEventsPaged(filters: MaybeRefOrGetter<PastEventFilters>) {
  const selected = computed(() => toValue(filters))

  return usePagedList({
    queryKey: () => {
      const { year, search, categoryId } = selected.value
      return eventQueryKeys.past(year, search, categoryId)
    },
    fetchPage: (page, pageSize) => getPastEventsPageRequest(selected.value, page, pageSize),
    enabled: () => selected.value.year !== '',
  })
}

/** Home page board: the featured event (or `null`) plus up to three other upcoming events. */
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

/** Public event detail; `notFound` becomes true once the API answers 404 for the id. */
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
