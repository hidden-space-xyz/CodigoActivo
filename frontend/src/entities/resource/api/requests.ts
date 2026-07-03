import {
  getApiResources,
  getApiResourcesResourceId,
} from '@/shared/api/generated/endpoints/resources/resources'
import type { ResourceResponse } from '@/shared/api/generated/models'
import { unwrapOrNull } from '@/shared/api'

import type { LearningResource } from '../model/types'
import { toLearningResource } from './mapper'

export async function getResourcesRequest(): Promise<readonly LearningResource[]> {
  const { data } = await getApiResources({ sort: '-createdAt', pageSize: 100 })
  return (data.items ?? []).map(toLearningResource)
}

export async function getResourceByIdRequest(id: string): Promise<LearningResource | null> {
  const response = await unwrapOrNull<ResourceResponse>(getApiResourcesResourceId(id))
  return response ? toLearningResource(response) : null
}
