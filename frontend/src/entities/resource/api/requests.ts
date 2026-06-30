import {
  getApiResources,
  getApiResourcesResourceId,
} from '@/shared/api/generated/endpoints/resources/resources'
import { ApiError } from '@/shared/api'

import type { LearningResource } from '../model/types'
import { toLearningResource } from './mapper'

export async function getResourcesRequest(): Promise<readonly LearningResource[]> {
  const response = await getApiResources()
  return (response.data ?? []).map(toLearningResource)
}

export async function getResourceByIdRequest(id: string): Promise<LearningResource | null> {
  try {
    const response = await getApiResourcesResourceId(id)
    return toLearningResource(response.data)
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return null
    throw error
  }
}
