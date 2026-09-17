import {
  deleteApiResourcesResourceId,
  getApiResources,
  getApiResourcesResourceId,
  postApiResources,
  putApiResourcesResourceId,
} from '@/shared/api/generated/endpoints/resources/resources'
import type {
  CreateResourceRequest,
  GetApiResourcesParams,
  ResourceListItemResponse,
  ResourceResponse,
  UpdateResourceRequest,
} from '@/shared/api/generated/models'
import { toPage, unwrapOrNull } from '@/shared/api'
import type { PagedListPage } from '@/shared/lib'

import type { LearningResource, LearningResourceSummary } from '../model/types'
import { toLearningResource, toLearningResourceSummary } from './mapper'

/** Fetches one page of resources, newest first; an empty `search` is not sent. */
export async function getResourcesPageRequest(
  search: string,
  page: number,
  pageSize: number,
): Promise<PagedListPage<LearningResourceSummary>> {
  const result = await getApiResources({
    ...(search ? { search } : {}),
    sort: '-createdAt',
    page,
    pageSize,
  })
  const { items, total } = toPage(result)
  return { items: items.map(toLearningResourceSummary), total }
}

/** Loads the public resource detail; resolves to `null` when it does not exist (404). */
export async function getResourceByIdRequest(id: string): Promise<LearningResource | null> {
  const response = await unwrapOrNull<ResourceResponse>(getApiResourcesResourceId(id))
  return response ? toLearningResource(response) : null
}

/** Fetches one page of the admin resources table with raw list items. */
export function getResourcesAdminPageRequest(
  params: GetApiResourcesParams,
): Promise<{ items: ResourceListItemResponse[]; total: number }> {
  return getApiResources(params).then(toPage)
}

/** Loads the raw resource for the admin editor; `null` when it does not exist (404). */
export function getResourceAdminRequest(id: string) {
  return unwrapOrNull<ResourceResponse>(getApiResourcesResourceId(id))
}

/** Creates a resource and resolves to the created resource. */
export function createResourceRequest(body: CreateResourceRequest) {
  return postApiResources(body).then((r) => r.data)
}

/** Replaces a resource and resolves to the updated resource. */
export function updateResourceRequest(id: string, body: UpdateResourceRequest) {
  return putApiResourcesResourceId(id, body).then((r) => r.data)
}

/** Deletes a resource (admin only); resolves with the raw 204 response. */
export function deleteResourceRequest(id: string) {
  return deleteApiResourcesResourceId(id)
}
