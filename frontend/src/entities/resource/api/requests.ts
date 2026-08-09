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

export async function getResourcesPageRequest(
  page: number,
  pageSize: number,
): Promise<PagedListPage<LearningResourceSummary>> {
  const result = await getApiResources({ sort: '-createdAt', page, pageSize })
  const { items, total } = toPage(result)
  return { items: items.map(toLearningResourceSummary), total }
}

export async function getResourceByIdRequest(id: string): Promise<LearningResource | null> {
  const response = await unwrapOrNull<ResourceResponse>(getApiResourcesResourceId(id))
  return response ? toLearningResource(response) : null
}

export function getResourcesAdminPageRequest(
  params: GetApiResourcesParams,
): Promise<{ items: ResourceListItemResponse[]; total: number }> {
  return getApiResources(params).then(toPage)
}

export function getResourceAdminRequest(id: string) {
  return unwrapOrNull<ResourceResponse>(getApiResourcesResourceId(id))
}

export function createResourceRequest(body: CreateResourceRequest) {
  return postApiResources(body).then((r) => r.data)
}

export function updateResourceRequest(id: string, body: UpdateResourceRequest) {
  return putApiResourcesResourceId(id, body).then((r) => r.data)
}

export function deleteResourceRequest(id: string) {
  return deleteApiResourcesResourceId(id)
}
