import { computed, toValue, type MaybeRefOrGetter } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { resourceQueryKeys } from './query-keys'
import { getResourceByIdRequest, getResourcesRequest } from './requests'

export function useResources() {
  const query = useQuery({
    queryKey: resourceQueryKeys.all,
    queryFn: () => getResourcesRequest(),
  })

  return {
    resources: query.data,
    isLoading: query.isLoading,
    isError: query.isError,
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
