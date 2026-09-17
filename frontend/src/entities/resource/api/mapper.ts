import type { ResourceListItemResponse, ResourceResponse } from '@/shared/api/generated/models'
import { formatDate } from '@/shared/lib'

import type { LearningResource, LearningResourceSummary } from '../model/types'

/** Maps a list item to a card model; `date` is the formatted creation date. */
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

/** Maps the full resource to the detail model (summary fields plus description). */
export function toLearningResource(response: ResourceResponse): LearningResource {
  return {
    ...toLearningResourceSummary(response),
    description: response.description ?? '',
  }
}
