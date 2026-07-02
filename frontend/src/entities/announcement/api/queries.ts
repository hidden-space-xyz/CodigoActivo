import { computed, ref, toValue, watch, type MaybeRefOrGetter } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { announcementQueryKeys } from './query-keys'
import {
  getAnnouncementByIdRequest,
  getAnnouncementYearsRequest,
  getAnnouncementsByYearRequest,
  getHomeAnnouncementsRequest,
} from './requests'

/**
 * Public announcements model: a forced year filter (by year of `createdAt`), defaulting to the
 * latest year with data, showing that year's announcements newest-first with no pagination.
 */
export function useAnnouncements() {
  const yearsQuery = useQuery({
    queryKey: announcementQueryKeys.years(),
    queryFn: () => getAnnouncementYearsRequest(),
  })

  const years = computed(() => yearsQuery.data.value ?? [])
  const selectedYear = ref('')

  // Default to the latest year with data once it loads (and keep a valid selection).
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

  const byYearQuery = useQuery({
    queryKey: computed(() => announcementQueryKeys.byYear(selectedYear.value)),
    queryFn: () => getAnnouncementsByYearRequest(selectedYear.value),
    enabled: computed(() => selectedYear.value !== ''),
  })

  const isLoading = computed(
    () => yearsQuery.isLoading.value || (selectedYear.value !== '' && byYearQuery.isLoading.value),
  )

  return {
    years,
    selectedYear,
    setYear,
    announcements: byYearQuery.data,
    isLoading,
    isError: computed(() => yearsQuery.isError.value || byYearQuery.isError.value),
  }
}

/** Home block: one featured announcement plus up to three other recent ones. */
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
