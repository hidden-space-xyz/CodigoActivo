import { mutationOptions } from '@tanstack/vue-query'

import type { NewsItemInput } from '../model/types'
import { newsKeys } from './queries'
import {
  createNewsItemRequest,
  deleteNewsItemRequest,
  featureNewsItemRequest,
  updateNewsItemRequest,
} from './requests'

/** Mutation options of news; each one refreshes every news query once it succeeds. */
export const newsMutations = {
  create: () =>
    mutationOptions({
      mutationFn: (input: NewsItemInput) => createNewsItemRequest(input),
      meta: { invalidates: [newsKeys.all] },
    }),
  update: () =>
    mutationOptions({
      mutationFn: ({ id, input }: { id: string; input: NewsItemInput }) =>
        updateNewsItemRequest(id, input),
      meta: { invalidates: [newsKeys.all] },
    }),
  remove: () =>
    mutationOptions({
      mutationFn: (id: string) => deleteNewsItemRequest(id),
      meta: { invalidates: [newsKeys.all] },
    }),
  /** Makes one news item the featured one, unfeaturing the rest. */
  feature: () =>
    mutationOptions({
      mutationFn: (id: string) => featureNewsItemRequest(id),
      meta: { invalidates: [newsKeys.all] },
    }),
}
