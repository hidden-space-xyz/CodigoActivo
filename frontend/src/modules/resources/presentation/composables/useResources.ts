import { computed, toValue, type MaybeRefOrGetter } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { getResourceById } from '@/modules/resources/application/use-cases/get-resource-by-id.use-case'
import { getResources } from '@/modules/resources/application/use-cases/get-resources.use-case'
import { resourceRepository } from '@/modules/resources/infrastructure/repositories/resource-repository.provider'

const resourceQueryKeys = {
  all: ['resources'] as const,
  detail: (id: string) => ['resources', id] as const,
}

export function useResources() {
  const query = useQuery({
    queryKey: resourceQueryKeys.all,
    queryFn: () => getResources(resourceRepository),
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
    queryFn: () => getResourceById(resourceRepository, id.value),
  })

  const notFound = computed(() => !query.isLoading.value && query.data.value === null)

  return {
    resource: query.data,
    isLoading: query.isLoading,
    isError: query.isError,
    notFound,
  }
}
