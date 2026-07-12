import {
  deleteApiEventsCategoryTypeEventCategoryTypeId,
  getApiEventsCategoryType,
  postApiEventsCategoryType,
  putApiEventsCategoryTypeEventCategoryTypeId,
} from '@/shared/api/generated/endpoints/events/events'
import type {
  EventCategoryTypeResponse,
  GetApiEventsCategoryTypeParams,
} from '@/shared/api/generated/models'
import { toPage } from '@/shared/api'
import { useServerTable } from '@/shared/lib'
import { catalogQueryKeys } from '@/entities/catalog'

import { useCatalog } from './useCatalog'

export interface EventCategoryInput {
  name: string
  color: string
}

export function useEventCategories() {
  const table = useServerTable<EventCategoryTypeResponse, GetApiEventsCategoryTypeParams>({
    queryKey: [...catalogQueryKeys.eventCategoryTypes, 'table'],
    fetchPage: (params) => getApiEventsCategoryType(params).then(toPage),
    defaultSort: { field: 'name', order: 1 },
    columns: {
      name: { type: 'text' },
    },
  })

  const { create, update, remove } = useCatalog<EventCategoryInput>({
    queryKey: catalogQueryKeys.eventCategoryTypes,
    create: (body) => postApiEventsCategoryType(body),
    update: (id, body) => putApiEventsCategoryTypeEventCategoryTypeId(id, body),
    remove: (id) => deleteApiEventsCategoryTypeEventCategoryTypeId(id),
  })

  return { table, create, update, remove }
}
