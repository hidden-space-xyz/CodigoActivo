import { computed } from 'vue'
import { useInfiniteQuery } from '@tanstack/vue-query'

/** One page returned by `fetchPage`; `total` counts all matching items, not just this page. */
export interface PagedListPage<T> {
  readonly items: T[]
  readonly total: number
}

/**
 * A "load more" collection: the key its pages are cached under and how one 1-based page is
 * fetched. Entities expose their public lists as sources.
 */
export interface PagedListSource<T> {
  readonly queryKey: readonly unknown[]
  readonly fetchPage: (page: number, pageSize: number) => Promise<PagedListPage<T>>
}

interface UsePagedListOptions {
  readonly pageSize?: number | undefined
  readonly enabled?: (() => boolean) | undefined
}

/**
 * "Load more" list on top of an infinite query over the source that `source` returns; when it
 * depends on reactive filters and its key changes, the list starts over. Pages hold 25 items by
 * default, `items` concatenates every loaded page and `hasMore` turns false once `total` items are
 * loaded.
 */
export function usePagedList<T>(
  source: () => PagedListSource<T>,
  options: UsePagedListOptions = {},
) {
  const pageSize = options.pageSize ?? 25
  const current = computed(source)

  const listQuery = useInfiniteQuery({
    queryKey: computed(() => [...current.value.queryKey]),
    queryFn: ({ pageParam }) => current.value.fetchPage(pageParam, pageSize),
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
