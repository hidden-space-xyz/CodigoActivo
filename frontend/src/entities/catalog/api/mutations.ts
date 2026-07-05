import { useMutation, useQueryClient } from '@tanstack/vue-query'

import { postApiEventsCategoryType } from '@/shared/api/generated/endpoints/events/events'
import type { CreateEventCategoryTypeRequest } from '@/shared/api/generated/models'

import { catalogQueryKeys } from './query-keys'

export function useCreateEventCategoryType() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateEventCategoryTypeRequest) =>
      postApiEventsCategoryType(body).then((r) => r.data),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: catalogQueryKeys.eventCategoryTypes }),
  })
}
