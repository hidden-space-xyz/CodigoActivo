import type { LearningResource } from '@/modules/resources/domain/entities/learning-resource.entity'
import type { ResourceRepository } from '@/modules/resources/domain/repositories/resource-repository'

export function getResourceById(
  repository: ResourceRepository,
  id: string,
): Promise<LearningResource | null> {
  return repository.getById(id)
}
