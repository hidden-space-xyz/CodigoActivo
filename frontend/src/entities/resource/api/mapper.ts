import type {
  CreateResourceRequest,
  ResourceListItemResponse,
  ResourceResponse,
  ResourceTypeResponse,
} from '@/shared/api/generated/models'

import type {
  LearningResource,
  LearningResourceSummary,
  ResourceInput,
  ResourceType,
} from '../model/types'

/** Maps a resource type. */
export function toResourceType(type: ResourceTypeResponse): ResourceType {
  return { id: type.id, name: type.name, color: type.color, isExternal: type.isExternal }
}

/** Maps a resource of a list; a missing url becomes `null`. */
export function toLearningResourceSummary(
  response: ResourceListItemResponse,
): LearningResourceSummary {
  return {
    id: response.id,
    title: response.title,
    subtitle: response.subtitle,
    type: toResourceType(response.type),
    url: response.url ?? null,
    createdAt: response.createdAt,
    thumbnailId: response.thumbnailId,
  }
}

/** Maps a whole resource, adding the description to its summary. */
export function toLearningResource(response: ResourceResponse): LearningResource {
  return { ...toLearningResourceSummary(response), description: response.description }
}

/** Builds the body that creates or replaces a resource; both endpoints take the same fields. */
export function toResourceRequest(input: ResourceInput): CreateResourceRequest {
  return {
    title: input.title,
    subtitle: input.subtitle,
    description: input.description,
    url: input.url,
    resourceTypeId: input.resourceTypeId,
    thumbnailId: input.thumbnailId,
  }
}
