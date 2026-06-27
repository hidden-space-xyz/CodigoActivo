import type { LearningResource } from '@/modules/resources/domain/entities/learning-resource.entity'
import type { ResourceRepository } from '@/modules/resources/domain/repositories/resource-repository'
import { toLearningResource } from '@/modules/resources/infrastructure/mappers/resource.mapper'
import { getApiResources } from '@/shared/api/generated/endpoints/resources/resources'

export class HttpResourceRepository implements ResourceRepository {
  async getAll(): Promise<readonly LearningResource[]> {
    const response = await getApiResources()
    return (response.data ?? []).map(toLearningResource)
  }
}
