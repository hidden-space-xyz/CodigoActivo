import { mutationOptions } from '@tanstack/vue-query'

import type { EventInput } from '../model/types'
import { eventKeys } from './queries'
import {
  createEventRequest,
  deleteEventRequest,
  featureEventRequest,
  updateEventRequest,
} from './requests'

/** Mutation options of events; each one refreshes every event query once it succeeds. */
export const eventMutations = {
  create: () =>
    mutationOptions({
      mutationFn: (input: EventInput) => createEventRequest(input),
      meta: { invalidates: [eventKeys.all] },
    }),
  update: () =>
    mutationOptions({
      mutationFn: ({ id, input }: { id: string; input: EventInput }) =>
        updateEventRequest(id, input),
      meta: { invalidates: [eventKeys.all] },
    }),
  remove: () =>
    mutationOptions({
      mutationFn: (id: string) => deleteEventRequest(id),
      meta: { invalidates: [eventKeys.all] },
    }),
  /** Makes one event the featured one, unfeaturing the rest. */
  feature: () =>
    mutationOptions({
      mutationFn: (id: string) => featureEventRequest(id),
      meta: { invalidates: [eventKeys.all] },
    }),
}
