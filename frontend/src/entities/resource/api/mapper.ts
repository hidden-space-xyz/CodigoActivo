import type { ResourceListItemResponse, ResourceResponse } from '@/shared/api/generated/models'
import { formatDate } from '@/shared/lib'

import type { LearningResource, LearningResourceSummary } from '../model/types'

export function toLearningResourceSummary(
  response: ResourceListItemResponse,
): LearningResourceSummary {
  return {
    id: response.id ?? '',
    title: response.title ?? '',
    subtitle: response.subtitle ?? '',
    date: response.createdAt ? formatDate(response.createdAt) : '',
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
