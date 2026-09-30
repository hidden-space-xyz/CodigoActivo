import { mutationOptions } from '@tanstack/vue-query'

import type { EventCategoryInput } from '../model/types'
import { eventCategoryKeys } from './queries'
import {
  createEventCategoryRequest,
  deleteEventCategoryRequest,
  updateEventCategoryRequest,
} from './requests'

/** Mutation options of event categories; each one refreshes every category query on success. */
export const eventCategoryMutations = {
  /** Creates a category and resolves to it, so a form can select it right away. */
  create: () =>
    mutationOptions({
      mutationFn: (input: EventCategoryInput) => createEventCategoryRequest(input),
      meta: { invalidates: [eventCategoryKeys.all] },
    }),
  update: () =>
    mutationOptions({
      mutationFn: ({ id, input }: { id: string; input: EventCategoryInput }) =>
        updateEventCategoryRequest(id, input),
      meta: { invalidates: [eventCategoryKeys.all] },
    }),
  remove: () =>
    mutationOptions({
      mutationFn: (id: string) => deleteEventCategoryRequest(id),
      meta: { invalidates: [eventCategoryKeys.all] },
    }),
}
