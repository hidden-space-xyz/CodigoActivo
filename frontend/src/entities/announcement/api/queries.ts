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

  const byYearList = usePagedList({
    queryKey: () => announcementQueryKeys.byYear(selectedYear.value),
    fetchPage: (page, pageSize) =>
      getAnnouncementsByYearPageRequest(selectedYear.value, page, pageSize),
    enabled: () => selectedYear.value !== '',
  })

  const isLoading = computed(
    () => yearsQuery.isLoading.value || (selectedYear.value !== '' && byYearList.isLoading.value),
  )

  return {
    years,
    selectedYear,
    setYear,
    announcements: byYearList.items,
    hasMore: byYearList.hasMore,
    loadMore: byYearList.loadMore,
    isFetchingMore: byYearList.isFetchingMore,
    isLoading,
    isError: computed(() => yearsQuery.isError.value || byYearList.isError.value),
  }
}

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
