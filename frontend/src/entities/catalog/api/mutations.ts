import { useMutation, useQueryClient } from '@tanstack/vue-query'

import { postApiEventsCategoryType } from '@/shared/api/generated/endpoints/events/events'
import type { CreateEventCategoryTypeRequest } from '@/shared/api/generated/models'

import { catalogQueryKeys } from './query-keys'

/**
 * Creates an event category and refreshes the category list before resolving, so callers can
 * select the new category as soon as the mutation settles. Shared by the catalog admin section
 * and the inline "new category" flow in the event form.
 */
export function useCreateEventCategoryType() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateEventCategoryTypeRequest) =>
      postApiEventsCategoryType(body).then((r) => r.data),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: catalogQueryKeys.eventCategoryTypes }),
  })
}
