import type { LearningResource } from '@/modules/resources/domain/entities/learning-resource.entity'
import type { ResourceResponse } from '@/shared/api/generated/models'

export function toLearningResource(response: ResourceResponse): LearningResource {
  return {
    id: response.id ?? '',
    title: response.title ?? '',
    type: response.subtitle ?? '',
    description: response.description ?? '',
    thumbnailId: response.thumbnailId ?? '',
  }
}
