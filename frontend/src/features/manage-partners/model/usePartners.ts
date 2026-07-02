import { useMutation, useQueryClient } from '@tanstack/vue-query'

import {
  deleteApiPartnersPartnerId,
  postApiPartners,
  putApiPartnersPartnerId,
} from '@/shared/api/generated/endpoints/partners/partners'
import type {
  CreatePartnerRequest,
  PartnerResponse,
  UpdatePartnerRequest,
} from '@/shared/api/generated/models'
import { useODataTable } from '@/shared/lib'

const partnersListKey = ['partners'] as const

export function usePartners() {
  const queryClient = useQueryClient()
  const invalidate = () => queryClient.invalidateQueries({ queryKey: partnersListKey })

  const table = useODataTable<PartnerResponse>({
    resource: 'Partners',
    queryKey: [...partnersListKey, 'admin'],
    defaultSort: { field: 'tier', order: 1 },
    columns: {
      name: { type: 'text' },
      tier: { type: 'numeric' },
      website: { type: 'text' },
      fromDate: { type: 'date' },
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
