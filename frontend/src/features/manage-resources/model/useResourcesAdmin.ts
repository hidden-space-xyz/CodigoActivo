import { useMutation, useQueryClient } from '@tanstack/vue-query'

import {
  deleteApiResourcesResourceId,
  getApiResources,
  getApiResourcesResourceId,
  postApiResources,
  putApiResourcesResourceId,
} from '@/shared/api/generated/endpoints/resources/resources'
import type {
  CreateResourceRequest,
  GetApiResourcesParams,
  ResourceListItemResponse,
  ResourceResponse,
  UpdateResourceRequest,
} from '@/shared/api/generated/models'
import { toPage, unwrapOrNull } from '@/shared/api'
import { useServerTable } from '@/shared/lib'
import { resourceQueryKeys } from '@/entities/resource'

export function useResourcesAdmin() {
  const queryClient = useQueryClient()
  const invalidate = () => queryClient.invalidateQueries({ queryKey: resourceQueryKeys.all })

  const table = useServerTable<ResourceListItemResponse, GetApiResourcesParams>({
    queryKey: [...resourceQueryKeys.all, 'admin'],
    fetchPage: (params) => getApiResources(params).then(toPage),
    defaultSort: { field: 'createdAt', order: -1 },
    columns: {
      title: { type: 'text' },
      subtitle: { type: 'text' },
      type: { param: 'resourceTypeId' },
      url: { type: 'text' },
      created: { type: 'dateRange', fromParam: 'createdFrom', toParam: 'createdTo' },
    },
  })

  const create = useMutation({
    mutationFn: (body: CreateResourceRequest) => postApiResources(body).then((r) => r.data),
    onSuccess: invalidate,
  })

  const update = useMutation({
    mutationFn: (vars: { id: string; body: UpdateResourceRequest }) =>
      putApiResourcesResourceId(vars.id, vars.body).then((r) => r.data),
    onSuccess: invalidate,
  })

  const remove = useMutation({
    mutationFn: (id: string) => deleteApiResourcesResourceId(id),
    onSuccess: invalidate,
  })

  const fetchOne = (id: string) => unwrapOrNull<ResourceResponse>(getApiResourcesResourceId(id))

  return { table, create, update, remove, fetchOne }
}
