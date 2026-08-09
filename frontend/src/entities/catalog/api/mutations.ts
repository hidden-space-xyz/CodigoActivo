import { useMutation, useQueryClient } from '@tanstack/vue-query'

import type { CreateEventCategoryTypeRequest } from '@/shared/api/generated/models'

import { catalogQueryKeys } from './query-keys'
import { createEventCategoryTypeRequest } from './requests'

export function useCreateEventCategoryType() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateEventCategoryTypeRequest) => createEventCategoryTypeRequest(body),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: catalogQueryKeys.eventCategoryTypes() }),
  })
}
