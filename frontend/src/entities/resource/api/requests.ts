import {
  deleteApiResourcesResourceId,
  getApiResources,
  getApiResourcesResourceId,
  getApiResourcesTypes,
  postApiResources,
  putApiResourcesResourceId,
} from '@/shared/api/generated/endpoints/resources/resources'
import { toPage, unwrapOrNull } from '@/shared/api'
import type { PagedListPage, ServerTablePage } from '@/shared/lib/paging'

import type {
  LearningResource,
  LearningResourceSummary,
  ResourceInput,
  ResourceListParams,
  ResourceType,
} from '../model/types'
import {
  toLearningResource,
  toLearningResourceSummary,
  toResourceRequest,
  toResourceType,
} from './mapper'

/** Fetches one page of public resources, newest first; an empty `search` is not sent. */
export async function getResourcesPageRequest(
  search: string,
  page: number,
  pageSize: number,
): Promise<PagedListPage<LearningResourceSummary>> {
  const response = await getApiResources({
    ...(search ? { search } : {}),
    sort: '-createdAt',
    page,
    pageSize,
  })
  const { items, total } = toPage(response)
  return { items: items.map(toLearningResourceSummary), total }
}

/** Loads a whole resource; resolves to `null` when it does not exist (404). */
export async function getResourceRequest(id: string): Promise<LearningResource | null> {
  const response = await unwrapOrNull(getApiResourcesResourceId(id))
  return response ? toLearningResource(response) : null
}

/** Lists the resource types. */
export async function getResourceTypesRequest(): Promise<readonly ResourceType[]> {
  const response = await getApiResourcesTypes()
  return response.data.map(toResourceType)
}

/** Fetches one page of the admin resource list. */
export async function getResourceListPageRequest(
  params: ResourceListParams,
): Promise<ServerTablePage<LearningResourceSummary>> {
  const { items, total } = toPage(await getApiResources(params))
  return { items: items.map(toLearningResourceSummary), total }
}

/** Creates a resource. */
export async function createResourceRequest(input: ResourceInput): Promise<void> {
  await postApiResources(toResourceRequest(input))
}

/** Replaces a resource. */
export async function updateResourceRequest(id: string, input: ResourceInput): Promise<void> {
  await putApiResourcesResourceId(id, toResourceRequest(input))
}

/** Deletes a resource. */
export async function deleteResourceRequest(id: string): Promise<void> {
  await deleteApiResourcesResourceId(id)
}
