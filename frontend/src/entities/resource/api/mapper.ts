import type { ResourceListItemResponse, ResourceResponse } from '@/shared/api/generated/models'

import type { LearningResource, LearningResourceSummary } from '../model/types'

export function toLearningResourceSummary(
  response: ResourceListItemResponse,
): LearningResourceSummary {
  return {
    id: response.id ?? '',
    title: response.title ?? '',
    subtitle: response.subtitle ?? '',
    typeName: response.type?.name ?? '',
    typeColor: response.type?.color ?? '',
    url: response.url ?? null,
    thumbnailId: response.thumbnailId ?? '',
  }
}

export function toLearningResource(response: ResourceResponse): LearningResource {
  return {
    ...toLearningResourceSummary(response),
    description: response.description ?? '',
  }
}
