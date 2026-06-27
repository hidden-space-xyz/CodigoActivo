import type { LearningResource } from '@/modules/resources/domain/entities/learning-resource.entity'
import type { ResourceRepository } from '@/modules/resources/domain/repositories/resource-repository'

export function getResources(repository: ResourceRepository): Promise<readonly LearningResource[]> {
  return repository.getAll()
}
