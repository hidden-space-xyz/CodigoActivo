import type { TranslationKey } from '@/shared/i18n'

import type { EventDetail, EventStatusKind } from './types'

/** Whether someone may sign up for an event's activities right now, and why not otherwise. */
export type SignupAccess = 'open' | 'earlyOnly' | 'closed'

const STATUS_LABEL_KEYS: Record<EventStatusKind, TranslationKey> = {
  upcoming: 'entities.event.status.upcoming',
  earlySignupOpen: 'entities.event.status.earlySignupOpen',
  signupOpen: 'entities.event.status.signupOpen',
  signupClosed: 'entities.event.status.signupClosed',
  finished: 'entities.event.status.finished',
}

/** Translation key of the label shown for an event stage. */
export function statusLabelKey(status: EventStatusKind): TranslationKey {
  return STATUS_LABEL_KEYS[status]
}

/**
 * Signup access of a user to an event: open during the signup window, and during early signup
 * only for users the API deems eligible; the rest wait (`earlyOnly`) or are too late or early
 * (`closed`).
 */
export function signupAccess(
  event: Pick<EventDetail, 'status'>,
  earlySignupEligible: boolean,
): SignupAccess {
  if (event.status === 'signupOpen') return 'open'
  if (event.status !== 'earlySignupOpen') return 'closed'
  return earlySignupEligible ? 'open' : 'earlyOnly'
}

/**
 * Next instant after `now`, in epoch milliseconds, at which early signup opens, signup opens or
 * signup closes, so the stage of the event changes; `null` once all of them have passed.
 */
export function nextSignupChange(
  event: Pick<EventDetail, 'earlySignupStartsAt' | 'signupStartsAt' | 'signupEndsAt'>,
  now: number,
): number | null {
  const upcoming = [event.earlySignupStartsAt, event.signupStartsAt, event.signupEndsAt]
    .filter((instant): instant is string => instant !== null)
    .map((instant) => Date.parse(instant))
    .filter((time) => time > now)
  return upcoming.length > 0 ? Math.min(...upcoming) : null
}
