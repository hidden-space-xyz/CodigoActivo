import type { LearningResource } from '@/modules/resources/domain/entities/learning-resource.entity'
import type { ResourceRepository } from '@/modules/resources/domain/repositories/resource-repository'
import { toLearningResource } from '@/modules/resources/infrastructure/mappers/resource.mapper'
import {
  getApiResources,
  getApiResourcesResourceId,
} from '@/shared/api/generated/endpoints/resources/resources'
import { ApiError } from '@/shared/api/http-client'

export class HttpResourceRepository implements ResourceRepository {
  async getAll(): Promise<readonly LearningResource[]> {
    const response = await getApiResources()
    return (response.data ?? []).map(toLearningResource)
  }

  async getById(id: string): Promise<LearningResource | null> {
    try {
      const response = await getApiResourcesResourceId(id)
      return toLearningResource(response.data)
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) return null
      throw error
    }
  }
}
