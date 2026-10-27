import {
  deleteApiEventsCategoryTypeEventCategoryTypeId,
  getApiEventsCategoryType,
  postApiEventsCategoryType,
  putApiEventsCategoryTypeEventCategoryTypeId,
} from '@/shared/api/generated/endpoints/events/events'
import { toPage } from '@/shared/api'
import { MAX_PAGE_SIZE } from '@/shared/config'
import type { ServerTablePage } from '@/shared/lib/paging'

import type { EventCategory, EventCategoryInput, EventCategoryListParams } from '../model/types'
import { toEventCategory, toEventCategoryRequest } from './mapper'

/** Loads up to 100 categories for selectors and filters. */
export async function getEventCategoriesRequest(): Promise<readonly EventCategory[]> {
  const response = await getApiEventsCategoryType({ pageSize: MAX_PAGE_SIZE })
  return toPage(response, toEventCategory).items
}

/** Fetches one page of the admin category list. */
export async function getEventCategoryListPageRequest(
  params: EventCategoryListParams,
): Promise<ServerTablePage<EventCategory>> {
  return toPage(await getApiEventsCategoryType(params), toEventCategory)
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

/**
 * Deletes a category; the API removes it from the events that use it and refuses with a conflict
 * when it is the only category of one of them.
 */
export async function deleteEventCategoryRequest(id: string): Promise<void> {
  await deleteApiEventsCategoryTypeEventCategoryTypeId(id)
}
