import { computed, ref, toValue, watch, type MaybeRefOrGetter } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { usePagedList } from '@/shared/lib'

import { newsQueryKeys } from './query-keys'
import {
  getHomeNewsRequest,
  getNewsByYearPageRequest,
  getNewsItemByIdRequest,
  getNewsYearsRequest,
} from './requests'

/**
 * Drives the public news archive: loads the available years, keeps a valid year selected
 * (the first one by default) and pages that year's news items, newest first, filtered by search.
 */
export function useNews() {
  const yearsQuery = useQuery({
    queryKey: newsQueryKeys.years(),
    queryFn: () => getNewsYearsRequest(),
  })

  const years = computed(() => yearsQuery.data.value ?? [])
  const selectedYear = ref('')

  watch(
    years,
    (list) => {
      if (!list.length) {
        selectedYear.value = ''
        return
      }
      if (!list.includes(selectedYear.value)) selectedYear.value = list[0] ?? ''
    },
    { immediate: true },
  )

  function setYear(year: string): void {
    selectedYear.value = year
  }

  const search = ref('')

  function setSearch(value: string): void {
    search.value = value
  }

  const byYearList = usePagedList({
    queryKey: () => newsQueryKeys.byYear(selectedYear.value, search.value),
    fetchPage: (page, pageSize) =>
      getNewsByYearPageRequest(selectedYear.value, search.value, page, pageSize),
    enabled: () => selectedYear.value !== '',
  })

  const isLoading = computed(
    () => yearsQuery.isLoading.value || (selectedYear.value !== '' && byYearList.isLoading.value),
  )

  return {
    years,
    selectedYear,
    setYear,
    search,
    setSearch,
    news: byYearList.items,
    hasMore: byYearList.hasMore,
    loadMore: byYearList.loadMore,
    isFetchingMore: byYearList.isFetchingMore,
    isLoading,
    isError: computed(() => yearsQuery.isError.value || byYearList.isError.value),
  }
}

/** Home page block: up to four news items, featured first; `featured` is `null` if none. */
export function useHomeNews() {
  const query = useQuery({
    queryKey: newsQueryKeys.home(),
    queryFn: () => getHomeNewsRequest(),
  })

  return {
    featured: computed(() => query.data.value?.featured ?? null),
    items: computed(() => query.data.value?.items ?? []),
    isLoading: query.isLoading,
    isError: query.isError,
  }
}

/** Loads a public news item by id; `notFound` turns true once the API answers 404. */
export function useNewsDetail(newsItemId: MaybeRefOrGetter<string>) {
  const id = computed(() => toValue(newsItemId))

  const query = useQuery({
    queryKey: computed(() => newsQueryKeys.publicDetail(id.value)),
    queryFn: () => getNewsItemByIdRequest(id.value),
  })

  const notFound = computed(() => !query.isLoading.value && query.data.value === null)

  return {
    newsItem: query.data,
    isLoading: query.isLoading,
    isError: query.isError,
    notFound,
  }
}
