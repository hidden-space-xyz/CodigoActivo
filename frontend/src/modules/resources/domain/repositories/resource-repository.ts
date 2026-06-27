import type { LearningResource } from '@/modules/resources/domain/entities/learning-resource.entity'

export interface ResourceRepository {
  getAll(): Promise<readonly LearningResource[]>
  getById(id: string): Promise<LearningResource | null>
}
