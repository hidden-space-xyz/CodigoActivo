import { computed, ref, toValue, watch, type MaybeRefOrGetter } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { usePagedList } from '@/shared/lib'

import { announcementQueryKeys } from './query-keys'
import {
  getAnnouncementByIdRequest,
  getAnnouncementsByYearPageRequest,
  getAnnouncementYearsRequest,
  getHomeAnnouncementsRequest,
} from './requests'

/**
 * Drives the public announcements archive: loads the available years, keeps a valid year selected
 * (the first one by default) and pages that year's announcements, newest first, filtered by search.
 */
export function useAnnouncements() {
  const yearsQuery = useQuery({
    queryKey: announcementQueryKeys.years(),
    queryFn: () => getAnnouncementYearsRequest(),
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
    queryKey: () => announcementQueryKeys.byYear(selectedYear.value, search.value),
    fetchPage: (page, pageSize) =>
      getAnnouncementsByYearPageRequest(selectedYear.value, search.value, page, pageSize),
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
    announcements: byYearList.items,
    hasMore: byYearList.hasMore,
    loadMore: byYearList.loadMore,
    isFetchingMore: byYearList.isFetchingMore,
    isLoading,
    isError: computed(() => yearsQuery.isError.value || byYearList.isError.value),
  }
}

/** Home page block: up to four announcements, featured first; `featured` is `null` if none. */
export function useHomeAnnouncements() {
  const query = useQuery({
    queryKey: announcementQueryKeys.home(),
    queryFn: () => getHomeAnnouncementsRequest(),
  })

  return {
    featured: computed(() => query.data.value?.featured ?? null),
    items: computed(() => query.data.value?.items ?? []),
    isLoading: query.isLoading,
    isError: query.isError,
  }
}

/** Loads a public announcement by id; `notFound` turns true once the API answers 404. */
export function useAnnouncementDetail(announcementId: MaybeRefOrGetter<string>) {
  const id = computed(() => toValue(announcementId))

  const query = useQuery({
    queryKey: computed(() => announcementQueryKeys.publicDetail(id.value)),
    queryFn: () => getAnnouncementByIdRequest(id.value),
  })

  const notFound = computed(() => !query.isLoading.value && query.data.value === null)

  return {
    announcement: query.data,
    isLoading: query.isLoading,
    isError: query.isError,
    notFound,
  }
}
