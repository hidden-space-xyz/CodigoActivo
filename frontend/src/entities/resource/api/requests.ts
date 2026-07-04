import {
  getApiResources,
  getApiResourcesResourceId,
} from '@/shared/api/generated/endpoints/resources/resources'
import type { ResourceResponse } from '@/shared/api/generated/models'
import { fetchAllPages, toPage, unwrapOrNull } from '@/shared/api'

import type { LearningResource } from '../model/types'
import { toLearningResource } from './mapper'

export async function getResourcesRequest(): Promise<readonly LearningResource[]> {
  const items = await fetchAllPages<ResourceResponse>((page, pageSize) =>
    getApiResources({ sort: '-createdAt', page, pageSize }).then(toPage),
  )
  return items.map(toLearningResource)
}

export async function getResourceByIdRequest(id: string): Promise<LearningResource | null> {
  const response = await unwrapOrNull<ResourceResponse>(getApiResourcesResourceId(id))
  return response ? toLearningResource(response) : null
}
