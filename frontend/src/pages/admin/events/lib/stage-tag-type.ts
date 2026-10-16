import type { EventStatusKind } from '@/entities/event'

type StageTagType = 'info' | 'warning' | 'success' | 'primary'

const STAGE_TAG_TYPES: Record<EventStatusKind, StageTagType> = {
  upcoming: 'info',
  earlySignupOpen: 'warning',
  signupOpen: 'success',
  signupClosed: 'primary',
  finished: 'primary',
}

/**
 * Element Plus tag type showing an event stage: upcoming info, early signup warning, open success,
 * closed/finished neutral (the theme draws `primary` tags in neutral grey).
 */
export function stageTagType(status: EventStatusKind): StageTagType {
  return STAGE_TAG_TYPES[status]
}
