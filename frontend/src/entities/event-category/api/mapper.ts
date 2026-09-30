import type {
  CreateEventCategoryTypeRequest,
  EventCategoryTypeResponse,
} from '@/shared/api/generated/models'

import type { EventCategory, EventCategoryInput } from '../model/types'

/** Maps an event category. */
export function toEventCategory(category: EventCategoryTypeResponse): EventCategory {
  return { id: category.id, name: category.name, color: category.color }
}

/** Builds the body that creates or updates a category; both endpoints take the same fields. */
export function toEventCategoryRequest(input: EventCategoryInput): CreateEventCategoryTypeRequest {
  return { name: input.name, color: input.color }
}
