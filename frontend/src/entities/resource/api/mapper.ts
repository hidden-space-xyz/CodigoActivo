import type { ResourceResponse } from '@/shared/api/generated/models'

import type { LearningResource } from '../model/types'

export function toLearningResource(response: ResourceResponse): LearningResource {
  return {
    id: response.id ?? '',
    title: response.title ?? '',
    type: response.subtitle ?? '',
    description: response.description ?? '',
    thumbnailId: response.thumbnailId ?? '',
  }
}
