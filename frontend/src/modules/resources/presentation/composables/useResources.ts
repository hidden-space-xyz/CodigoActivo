import { useQuery } from '@tanstack/vue-query'

import { getResources } from '@/modules/resources/application/use-cases/get-resources.use-case'
import { resourceRepository } from '@/modules/resources/infrastructure/repositories/resource-repository.provider'

const resourceQueryKeys = {
  all: ['resources'] as const,
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
