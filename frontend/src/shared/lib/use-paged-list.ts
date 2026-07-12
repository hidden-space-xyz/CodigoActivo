import { computed } from 'vue'
import { useInfiniteQuery } from '@tanstack/vue-query'

export interface PagedListPage<T> {
  readonly items: T[]
  readonly total: number
}

interface UsePagedListOptions<T> {
  readonly queryKey: () => readonly unknown[]
  readonly fetchPage: (page: number, pageSize: number) => Promise<PagedListPage<T>>
  readonly pageSize?: number | undefined
  readonly enabled?: (() => boolean) | undefined
}

export function usePagedList<T>(options: UsePagedListOptions<T>) {
  const pageSize = options.pageSize ?? 25

  const listQuery = useInfiniteQuery({
    queryKey: computed(() => [...options.queryKey()]),
    queryFn: ({ pageParam }) => options.fetchPage(pageParam, pageSize),
    initialPageParam: 1,
    getNextPageParam: (lastPage, allPages) => {
      const loaded = allPages.reduce((count, page) => count + page.items.length, 0)
      return loaded < lastPage.total ? allPages.length + 1 : undefined
    },
    enabled: computed(() => options.enabled?.() ?? true),
  })

  const pages = computed(() => listQuery.data.value?.pages ?? [])

  function loadMore(): void {
    void listQuery.fetchNextPage()
  }

  return {
    items: computed<T[]>(() => pages.value.flatMap((page) => page.items)),
    total: computed(() => pages.value[pages.value.length - 1]?.total ?? 0),
    hasMore: computed(() => listQuery.hasNextPage.value),
    loadMore,
    isLoading: listQuery.isLoading,
    isFetchingMore: listQuery.isFetchingNextPage,
    isError: listQuery.isError,
  }
}
