import { DEFAULT_TAG_COLOR, toHexColor } from '@/shared/lib/color'
import type { FormReading } from '@/shared/lib/form'

import type { EventCategory, EventCategoryInput } from './types'

/** What a category form binds its inputs to; `color` may lack its leading `#`. */
export interface EventCategoryDraft {
  name: string
  color: string
}

/** A draft with the default color, or one filled with the category being edited. */
export function toEventCategoryDraft(category: EventCategory | null): EventCategoryDraft {
  return { name: category?.name ?? '', color: category?.color ?? DEFAULT_TAG_COLOR }
}

/** Reads a category form: a name is required and the color is sent as `#` hex. */
export function readEventCategoryDraft(
  draft: EventCategoryDraft,
): FormReading<'name', EventCategoryInput> {
  const name = draft.name.trim()
  if (!name) return { problems: { name: true }, value: null }
  return { problems: {}, value: { name, color: toHexColor(draft.color) } }
}
