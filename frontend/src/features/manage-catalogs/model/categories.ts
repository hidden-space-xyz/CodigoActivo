import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

import {
  deleteApiEventsCategoryTypeEventCategoryTypeId,
  postApiEventsCategoryType,
  putApiEventsCategoryTypeEventCategoryTypeId,
} from '@/shared/api/generated/endpoints/events/events'
import type { EventCategoryTypeResponse } from '@/shared/api/generated/models'
import { fetchODataList } from '@/shared/api'
import { catalogQueryKeys } from '@/entities/catalog'

export interface EventCategoryInput {
  name: string
  color: string
}

export function useEventCategories() {
  const queryClient = useQueryClient()
  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: catalogQueryKeys.eventCategoryTypes })

  const list = useQuery({
    queryKey: catalogQueryKeys.eventCategoryTypes,
    queryFn: () =>
      fetchODataList<EventCategoryTypeResponse>('EventCategoryTypes', {
        orderBy: 'name asc',
        top: 100,
      }).then((r) => r.items),
  })

  const create = useMutation({
    mutationFn: (body: EventCategoryInput) => postApiEventsCategoryType(body),
    onSuccess: invalidate,
  })

  const update = useMutation({
    mutationFn: (vars: { id: string; body: EventCategoryInput }) =>
      putApiEventsCategoryTypeEventCategoryTypeId(vars.id, vars.body),
    onSuccess: invalidate,
  })

  const remove = useMutation({
    mutationFn: (id: string) => deleteApiEventsCategoryTypeEventCategoryTypeId(id),
    onSuccess: invalidate,
  })

  return { list, create, update, remove }
}
