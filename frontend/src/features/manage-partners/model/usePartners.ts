import { useMutation, useQueryClient } from '@tanstack/vue-query'

import type {
  CreatePartnerRequest,
  GetApiPartnersParams,
  PartnerResponse,
  UpdatePartnerRequest,
} from '@/shared/api/generated/models'
import { useServerTable } from '@/shared/lib'
import {
  createPartnerRequest,
  deletePartnerRequest,
  getPartnersPageRequest,
  partnerQueryKeys,
  updatePartnerRequest,
} from '@/entities/partner'

export function usePartners() {
  const queryClient = useQueryClient()
  const invalidate = () => queryClient.invalidateQueries({ queryKey: partnerQueryKeys.all })

  const table = useServerTable<PartnerResponse, GetApiPartnersParams>({
    queryKey: partnerQueryKeys.adminTable(),
    fetchPage: (params) => getPartnersPageRequest(params),
    defaultSort: { field: 'tier', order: 1 },
    columns: {
      name: { type: 'text' },
      tier: { type: 'number' },
      website: { type: 'text' },
      fromDate: { type: 'dateRange', fromParam: 'fromDateFrom', toParam: 'fromDateTo' },
    },
  })

  const create = useMutation({
    mutationFn: (body: CreatePartnerRequest) => createPartnerRequest(body),
    onSuccess: invalidate,
  })

  const update = useMutation({
    mutationFn: (vars: { id: string; body: UpdatePartnerRequest }) =>
      updatePartnerRequest(vars.id, vars.body),
    onSuccess: invalidate,
  })

  const remove = useMutation({
    mutationFn: (id: string) => deletePartnerRequest(id),
    onSuccess: invalidate,
  })

  return { table, create, update, remove }
}
