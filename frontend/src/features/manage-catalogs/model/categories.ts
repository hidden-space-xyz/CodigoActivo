import {
  deleteApiEventsCategoryTypeEventCategoryTypeId,
  getApiEventsCategoryType,
  postApiEventsCategoryType,
  putApiEventsCategoryTypeEventCategoryTypeId,
} from '@/shared/api/generated/endpoints/events/events'
import type { EventCategoryTypeResponse } from '@/shared/api/generated/models'
import { catalogQueryKeys } from '@/entities/catalog'

import { useCatalog } from './useCatalog'

export interface EventCategoryInput {
  name: string
  color: string
}

export function useEventCategories() {
  return useCatalog<EventCategoryTypeResponse, EventCategoryInput>({
    queryKey: catalogQueryKeys.eventCategoryTypes,
    fetchAll: () => getApiEventsCategoryType().then((r) => r.data ?? []),
    create: (body) => postApiEventsCategoryType(body),
    update: (id, body) => putApiEventsCategoryTypeEventCategoryTypeId(id, body),
    remove: (id) => deleteApiEventsCategoryTypeEventCategoryTypeId(id),
  })
}
