import { computed, ref, watch } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { newsPages, newsQueries } from '@/entities/news-item'
import { usePagedList } from '@/shared/lib/paging'

/**
 * Public news archive: the years that have news, a selected year that stays valid (the newest by
 * default) and that year's news items, newest first, filtered by the search text and loaded page
 * by page. Call it in `setup`.
 */
export function useNewsArchive() {
  const yearsQuery = useQuery(newsQueries.years())
  const years = computed(() => yearsQuery.data.value ?? [])
  const selectedYear = ref('')
  const search = ref('')

  watch(
    years,
    (list) => {
      if (!list.includes(selectedYear.value)) selectedYear.value = list[0] ?? ''
    },
    { immediate: true },
  )

  function setYear(year: string): void {
    selectedYear.value = year
  }

  function setSearch(value: string): void {
    search.value = value
  }

  const list = usePagedList(() => newsPages(selectedYear.value, search.value), {
    enabled: () => selectedYear.value !== '',
  })

  const isLoading = computed(
    () => yearsQuery.isLoading.value || (selectedYear.value !== '' && list.isLoading.value),
  )

  return {
    years,
    selectedYear,
    setYear,
    search,
    setSearch,
    news: list.items,
    hasMore: list.hasMore,
    loadMore: list.loadMore,
    isFetchingMore: list.isFetchingMore,
    isLoading,
  }
}
