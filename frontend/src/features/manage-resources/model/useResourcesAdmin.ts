import { useMutation, useQueryClient } from '@tanstack/vue-query'

import type {
  CreateResourceRequest,
  GetApiResourcesParams,
  ResourceListItemResponse,
  UpdateResourceRequest,
} from '@/shared/api/generated/models'
import { useServerTable } from '@/shared/lib'
import {
  createResourceRequest,
  deleteResourceRequest,
  getResourceAdminRequest,
  getResourcesAdminPageRequest,
  resourceQueryKeys,
  updateResourceRequest,
} from '@/entities/resource'

export function useResourcesAdmin() {
  const queryClient = useQueryClient()
  const invalidate = () => queryClient.invalidateQueries({ queryKey: resourceQueryKeys.all })

  const table = useServerTable<ResourceListItemResponse, GetApiResourcesParams>({
    queryKey: resourceQueryKeys.adminTable(),
    fetchPage: (params) => getResourcesAdminPageRequest(params),
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
    mutationFn: (body: CreateResourceRequest) => createResourceRequest(body),
    onSuccess: invalidate,
  })

  const update = useMutation({
    mutationFn: (vars: { id: string; body: UpdateResourceRequest }) =>
      updateResourceRequest(vars.id, vars.body),
    onSuccess: invalidate,
  })

  const remove = useMutation({
    mutationFn: (id: string) => deleteResourceRequest(id),
    onSuccess: invalidate,
  })

  const fetchOne = (id: string) => getResourceAdminRequest(id)

  return { table, create, update, remove, fetchOne }
}
