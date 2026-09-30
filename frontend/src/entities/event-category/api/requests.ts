import {
  deleteApiEventsCategoryTypeEventCategoryTypeId,
  getApiEventsCategoryType,
  postApiEventsCategoryType,
  putApiEventsCategoryTypeEventCategoryTypeId,
} from '@/shared/api/generated/endpoints/events/events'
import { toPage } from '@/shared/api'
import type { ServerTablePage } from '@/shared/lib/paging'

import type { EventCategory, EventCategoryInput, EventCategoryListParams } from '../model/types'
import { toEventCategory, toEventCategoryRequest } from './mapper'

/** Loads up to 100 categories for selectors and filters. */
export async function getEventCategoriesRequest(): Promise<readonly EventCategory[]> {
  const response = await getApiEventsCategoryType({ pageSize: 100 })
  return toPage(response).items.map(toEventCategory)
}

/** Fetches one page of the admin category list. */
export async function getEventCategoryListPageRequest(
  params: EventCategoryListParams,
): Promise<ServerTablePage<EventCategory>> {
  const { items, total } = toPage(await getApiEventsCategoryType(params))
  return { items: items.map(toEventCategory), total }
}

/** Creates a category and resolves to it. */
export async function createEventCategoryRequest(
  input: EventCategoryInput,
): Promise<EventCategory> {
  const response = await postApiEventsCategoryType(toEventCategoryRequest(input))
  return toEventCategory(response.data)
}

/** Renames or recolors a category. */
export async function updateEventCategoryRequest(
  id: string,
  input: EventCategoryInput,
): Promise<void> {
  await putApiEventsCategoryTypeEventCategoryTypeId(id, toEventCategoryRequest(input))
}

/** Deletes a category; the API removes it from the events that use it. */
export async function deleteEventCategoryRequest(id: string): Promise<void> {
  await deleteApiEventsCategoryTypeEventCategoryTypeId(id)
}
