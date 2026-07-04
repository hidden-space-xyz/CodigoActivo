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
import { toPage } from '@/shared/api'
import { useServerTable } from '@/shared/lib'
import { deleteThumbnail } from '@/entities/file'
import { partnerQueryKeys } from '@/entities/partner'

export function usePartners() {
  const queryClient = useQueryClient()
  // Invalidating the entity root also refreshes the public sponsors query after admin edits.
  const invalidate = () => queryClient.invalidateQueries({ queryKey: partnerQueryKeys.all })

  const table = useServerTable<PartnerResponse, GetApiPartnersParams>({
    queryKey: [...partnerQueryKeys.all, 'admin'],
    fetchPage: (params) => getApiPartners(params).then(toPage),
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
    mutationFn: (vars: { id: string; thumbnailId?: string | null | undefined }) =>
      deleteApiPartnersPartnerId(vars.id),
    onSuccess: (_data, vars) => {
      void deleteThumbnail(vars.thumbnailId)
      return invalidate()
    },
  })

  return { table, create, update, remove }
}
