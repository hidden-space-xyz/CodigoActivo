import {
  deleteApiResourcesResourceId,
  getApiResources,
  getApiResourcesResourceId,
  postApiResources,
  putApiResourcesResourceId,
} from '@/shared/api/generated/endpoints/resources/resources'
import { queryKeys } from '@/shared/api/query-keys'
import { useContentEntity } from '@/features/content/useContentEntity'

export function useResourcesAdmin() {
  return useContentEntity({
    queryKey: queryKeys.resources,
    fetchAll: (signal) => getApiResources({ signal }).then((r) => r.data ?? []),
    fetchOne: (id) => getApiResourcesResourceId(id).then((r) => r.data),
    create: (body) => postApiResources(body),
    update: (id, body) => putApiResourcesResourceId(id, body),
    remove: (id) => deleteApiResourcesResourceId(id),
  })
}
