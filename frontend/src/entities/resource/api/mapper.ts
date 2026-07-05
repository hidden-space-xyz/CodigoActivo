import type { ResourceListItemResponse, ResourceResponse } from '@/shared/api/generated/models'

import type { LearningResource, LearningResourceSummary } from '../model/types'

export function toLearningResourceSummary(
  response: ResourceListItemResponse,
): LearningResourceSummary {
  return {
    id: response.id ?? '',
    title: response.title ?? '',
    type: response.subtitle ?? '',
    thumbnailId: response.thumbnailId ?? '',
  }
}

export function toLearningResource(response: ResourceResponse): LearningResource {
  return {
    ...toLearningResourceSummary(response),
    description: response.description ?? '',
  }
}
