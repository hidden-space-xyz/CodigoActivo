import type { FormReading } from '@/shared/lib/form'

import type { EventCategory, EventCategoryInput } from './types'

/** Color a new category starts with. */
export const DEFAULT_CATEGORY_COLOR = '#6366F1'

/** What a category form binds its inputs to; `color` may lack its leading `#`. */
export interface EventCategoryDraft {
  name: string
  color: string
}

/** Writes a color as `#` followed by its hex digits. */
export function toHexColor(color: string): string {
  return `#${color.replace(/^#/, '')}`
}

/** A draft with the default color, or one filled with the category being edited. */
export function toEventCategoryDraft(category: EventCategory | null): EventCategoryDraft {
  return { name: category?.name ?? '', color: category?.color ?? DEFAULT_CATEGORY_COLOR }
}

/** Reads a category form: a name is required and the color is sent as `#` hex. */
export function readEventCategoryDraft(
  draft: EventCategoryDraft,
): FormReading<'name', EventCategoryInput> {
  const name = draft.name.trim()
  if (!name) return { problems: { name: true }, value: null }
  return { problems: {}, value: { name, color: toHexColor(draft.color) } }
}
