import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

import {
  deleteApiPartnersPartnerId,
  getApiPartners,
  postApiPartners,
  putApiPartnersPartnerId,
} from '@/shared/api/generated/endpoints/partners/partners'
import type { CreatePartnerRequest, UpdatePartnerRequest } from '@/shared/api/generated/models'
import { partnerQueryKeys } from '@/entities/partner'

export function usePartners() {
  const queryClient = useQueryClient()
  const invalidate = () => queryClient.invalidateQueries({ queryKey: partnerQueryKeys.all })

  const list = useQuery({
    queryKey: partnerQueryKeys.all,
    queryFn: ({ signal }) => getApiPartners({ signal }).then((response) => response.data ?? []),
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

  return { list, create, update, remove }
}
