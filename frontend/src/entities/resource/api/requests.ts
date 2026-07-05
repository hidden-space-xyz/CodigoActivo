import {
  getApiResources,
  getApiResourcesResourceId,
} from '@/shared/api/generated/endpoints/resources/resources'
import type { ResourceListItemResponse, ResourceResponse } from '@/shared/api/generated/models'
import { fetchAllPages, toPage, unwrapOrNull } from '@/shared/api'

import type { LearningResource, LearningResourceSummary } from '../model/types'
import { toLearningResource, toLearningResourceSummary } from './mapper'

export async function getResourcesRequest(): Promise<readonly LearningResourceSummary[]> {
  const items = await fetchAllPages<ResourceListItemResponse>((page, pageSize) =>
    getApiResources({ sort: '-createdAt', page, pageSize }).then(toPage),
  )
  return items.map(toLearningResourceSummary)
}

export async function getResourceByIdRequest(id: string): Promise<LearningResource | null> {
  const response = await unwrapOrNull<ResourceResponse>(getApiResourcesResourceId(id))
  return response ? toLearningResource(response) : null
}
