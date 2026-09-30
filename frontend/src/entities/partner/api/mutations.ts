import { mutationOptions } from '@tanstack/vue-query'

import type { PartnerInput } from '../model/types'
import { partnerKeys } from './queries'
import { createPartnerRequest, deletePartnerRequest, updatePartnerRequest } from './requests'

/** Mutation options of partners; each one refreshes every partner query once it succeeds. */
export const partnerMutations = {
  create: () =>
    mutationOptions({
      mutationFn: (input: PartnerInput) => createPartnerRequest(input),
      meta: { invalidates: [partnerKeys.all] },
    }),
  update: () =>
    mutationOptions({
      mutationFn: ({ id, input }: { id: string; input: PartnerInput }) =>
        updatePartnerRequest(id, input),
      meta: { invalidates: [partnerKeys.all] },
    }),
  remove: () =>
    mutationOptions({
      mutationFn: (id: string) => deletePartnerRequest(id),
      meta: { invalidates: [partnerKeys.all] },
    }),
}
