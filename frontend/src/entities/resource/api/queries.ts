import { computed, toValue, type MaybeRefOrGetter } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { usePagedList } from '@/shared/lib'

import { resourceQueryKeys } from './query-keys'
import { getResourceByIdRequest, getResourcesPageRequest } from './requests'

export function useResources() {
  const list = usePagedList({
    queryKey: () => resourceQueryKeys.list(),
    fetchPage: (page, pageSize) => getResourcesPageRequest(page, pageSize),
  })

  return {
    resources: list.items,
    hasMore: list.hasMore,
    loadMore: list.loadMore,
    isFetchingMore: list.isFetchingMore,
    isLoading: list.isLoading,
    isError: list.isError,
  }
}

export function useResourceDetail(resourceId: MaybeRefOrGetter<string>) {
  const id = computed(() => toValue(resourceId))

  const query = useQuery({
    queryKey: computed(() => resourceQueryKeys.detail(id.value)),
    queryFn: () => getResourceByIdRequest(id.value),
  })

  const notFound = computed(() => !query.isLoading.value && query.data.value === null)

  return {
    resource: query.data,
    isLoading: query.isLoading,
    isError: query.isError,
    notFound,
  }
}
