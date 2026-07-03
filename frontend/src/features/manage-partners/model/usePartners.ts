import { useMutation, useQueryClient } from '@tanstack/vue-query'

import {
  deleteApiPartnersPartnerId,
  getApiPartners,
  postApiPartners,
  putApiPartnersPartnerId,
} from '@/shared/api/generated/endpoints/partners/partners'
import type {
  CreatePartnerRequest,
  GetApiPartnersParams,
  PartnerResponse,
  UpdatePartnerRequest,
} from '@/shared/api/generated/models'
import { useServerTable } from '@/shared/lib'

const partnersListKey = ['partners'] as const

export function usePartners() {
  const queryClient = useQueryClient()
  const invalidate = () => queryClient.invalidateQueries({ queryKey: partnersListKey })

  const table = useServerTable<PartnerResponse, GetApiPartnersParams>({
    queryKey: [...partnersListKey, 'admin'],
    fetchPage: (params) =>
      getApiPartners(params).then((r) => ({
        items: r.data.items ?? [],
        total: r.data.total ?? 0,
      })),
    defaultSort: { field: 'tier', order: 1 },
    columns: {
      name: { type: 'text' },
      tier: { type: 'number' },
      website: { type: 'text' },
    },
  })

  const create = useMutation({
    mutationFn: (body: CreatePartnerRequest) =>
      postApiPartners(body).then((response) => response.data),
    onSuccess: invalidate,
  })

  const update = useMutation({
    mutationFn: (vars: { id: string; body: UpdatePartnerRequest }) =>
      putApiPartnersPartnerId(vars.id, vars.body).then((response) => response.data),
    onSuccess: invalidate,
  })

  const remove = useMutation({
    mutationFn: (id: string) => deleteApiPartnersPartnerId(id),
    onSuccess: invalidate,
  })

  return { table, create, update, remove }
}
