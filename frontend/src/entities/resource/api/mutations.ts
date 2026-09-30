import { mutationOptions } from '@tanstack/vue-query'

import type { ResourceInput } from '../model/types'
import { resourceKeys } from './queries'
import { createResourceRequest, deleteResourceRequest, updateResourceRequest } from './requests'

/** Mutation options of resources; each one refreshes every resource query once it succeeds. */
export const resourceMutations = {
  create: () =>
    mutationOptions({
      mutationFn: (input: ResourceInput) => createResourceRequest(input),
      meta: { invalidates: [resourceKeys.all] },
    }),
  update: () =>
    mutationOptions({
      mutationFn: ({ id, input }: { id: string; input: ResourceInput }) =>
        updateResourceRequest(id, input),
      meta: { invalidates: [resourceKeys.all] },
    }),
  remove: () =>
    mutationOptions({
      mutationFn: (id: string) => deleteResourceRequest(id),
      meta: { invalidates: [resourceKeys.all] },
    }),
}
