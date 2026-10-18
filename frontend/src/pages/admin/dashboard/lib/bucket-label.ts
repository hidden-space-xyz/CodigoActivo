import { LOCALE } from '@/shared/config'
import { parseDateOnly } from '@/shared/lib/date'

const dayMonthFormatter = new Intl.DateTimeFormat(LOCALE, { day: 'numeric', month: 'short' })

const monthYearFormatter = new Intl.DateTimeFormat(LOCALE, { month: 'short', year: '2-digit' })

/**
 * Chart axis label for a date bucket: short month and year when `granularity` is `'month'`,
 * day and month otherwise. Unparseable input is returned unchanged.
 */
export function formatBucketLabel(iso: string, granularity: string): string {
  const date = parseDateOnly(iso)
  if (!date) return iso
  return granularity === 'month' ? monthYearFormatter.format(date) : dayMonthFormatter.format(date)
}
