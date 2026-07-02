import { fetchODataEntity, fetchODataList } from '@/shared/api'
import type { ResourceResponse } from '@/shared/api/generated/models'

import type { LearningResource } from '../model/types'
import { toLearningResource } from './mapper'

export async function getResourcesRequest(): Promise<readonly LearningResource[]> {
  const page = await fetchODataList<ResourceResponse>('Resources', {
    orderBy: 'createdAt desc',
    top: 1000,
  })
  return page.items.map(toLearningResource)
}

export async function getResourceByIdRequest(id: string): Promise<LearningResource | null> {
  const response = await fetchODataEntity<ResourceResponse>('Resources', id)
  return response ? toLearningResource(response) : null
}
