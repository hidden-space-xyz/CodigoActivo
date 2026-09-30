import { toDateOnly } from './format'

/** Whether `date` falls on a calendar day before `reference`; never without a reference. */
export function isDayBefore(date: Date, reference: Date | string | null | undefined): boolean {
  if (!reference) return false
  const day = typeof reference === 'string' ? reference.slice(0, 10) : toDateOnly(reference)
  return toDateOnly(date) < day
}

/** Whether `date` falls on a calendar day after `reference`; never without a reference. */
export function isDayAfter(date: Date, reference: Date | string | null | undefined): boolean {
  if (!reference) return false
  const day = typeof reference === 'string' ? reference.slice(0, 10) : toDateOnly(reference)
  return toDateOnly(date) > day
}
