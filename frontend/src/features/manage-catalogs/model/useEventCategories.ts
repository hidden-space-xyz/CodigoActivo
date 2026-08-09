import type {
  EventCategoryTypeResponse,
  GetApiEventsCategoryTypeParams,
} from '@/shared/api/generated/models'
import { useServerTable } from '@/shared/lib'
import {
  catalogQueryKeys,
  createEventCategoryTypeRequest,
  deleteEventCategoryTypeRequest,
  getEventCategoryTypesPageRequest,
  updateEventCategoryTypeRequest,
} from '@/entities/catalog'

import { useCatalog } from './useCatalog'

export interface EventCategoryInput {
  name: string
  color: string
}

export function useEventCategories() {
  const table = useServerTable<EventCategoryTypeResponse, GetApiEventsCategoryTypeParams>({
    queryKey: catalogQueryKeys.eventCategoryTypesTable(),
    fetchPage: (params) => getEventCategoryTypesPageRequest(params),
    defaultSort: { field: 'name', order: 1 },
    columns: {
      name: { type: 'text' },
      color: { type: 'text' },
    },
  })

  const { create, update, remove } = useCatalog<EventCategoryInput>({
    queryKey: catalogQueryKeys.eventCategoryTypes(),
    create: (body) => createEventCategoryTypeRequest(body),
    update: (id, body) => updateEventCategoryTypeRequest(id, body),
    remove: (id) => deleteEventCategoryTypeRequest(id),
  })

  return { table, create, update, remove }
}
