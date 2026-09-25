import { i18n } from '@/shared/i18n'

const dateTimeFormatter = new Intl.DateTimeFormat('es-ES', {
  dateStyle: 'medium',
  timeStyle: 'short',
})

const dateFormatter = new Intl.DateTimeFormat('es-ES', { dateStyle: 'medium' })

const timeFormatter = new Intl.DateTimeFormat('es-ES', { timeStyle: 'short' })

const dayMonthFormatter = new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'short' })

const monthYearFormatter = new Intl.DateTimeFormat('es-ES', { month: 'short', year: '2-digit' })

const numberFormatter = new Intl.NumberFormat('es-ES')

const megabyteFormatter = new Intl.NumberFormat('es-ES', {
  minimumFractionDigits: 1,
  maximumFractionDigits: 1,
})

const RANGE_SEPARATOR = '–'

const DATE_ONLY = /^\d{4}-\d{2}-\d{2}$/

const NATIONAL_ID_CONTROL_LETTERS = 'TRWAGMYFPDXBNJZSQVHLCKE'
const NIE_PREFIXES: Readonly<Record<string, string>> = { X: '0', Y: '1', Z: '2' }
const NATIONAL_ID_SHAPE = /^[XYZ\d]\d{7}[A-Z]$/

/**
 * Parses the `YYYY-MM-DD` prefix as a local-midnight `Date`, avoiding `new Date()`'s UTC shift.
 * Returns `null` for empty or malformed input.
 */
export function parseDateOnly(value?: string | null): Date | null {
  if (!value) return null
  const [year, month, day] = value.slice(0, 10).split('-').map(Number)
  if (!year || !month || !day) return null
  return new Date(year, month - 1, day)
}

/** Formats the local calendar date as `YYYY-MM-DD`, ignoring the time. */
export function toDateOnly(date: Date): string {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

/** Today's date as `YYYY-MM-DD` in UTC, which can differ from the local date around midnight. */
export function todayIso(): string {
  return new Date().toISOString().slice(0, 10)
}

/** The date `years` before now as UTC `YYYY-MM-DD`; used for the adult (18+) birth-date limit. */
export function yearsAgoIso(years: number): string {
  const date = new Date()
  date.setFullYear(date.getFullYear() - years)
  return date.toISOString().slice(0, 10)
}

/** Completed years since a birth date, or `null` when the date is missing or invalid. */
export function ageFrom(value?: Date | string | null): number | null {
  if (!value) return null
  const birth = value instanceof Date ? value : parseDateOnly(value)
  if (!birth || Number.isNaN(birth.getTime())) return null
  const now = new Date()
  let age = now.getFullYear() - birth.getFullYear()
  const monthDiff = now.getMonth() - birth.getMonth()
  if (monthDiff < 0 || (monthDiff === 0 && now.getDate() < birth.getDate())) age--
  return age
}

/** Joins first and last name, or `—` when both are empty. */
export function fullName(person: { firstName?: string | null; lastName?: string | null }): string {
  return `${person.firstName ?? ''} ${person.lastName ?? ''}`.trim() || '—'
}

/** Label/value pair for select inputs. */
export interface SelectOption {
  readonly label: string
  readonly value: string
}

/** Maps catalog `{ id, name }` items to select options; missing name shows `—`, missing id `''`. */
export function toSelectOptions(
  items?: readonly { id?: string | null; name?: string | null }[] | null,
): SelectOption[] {
  return (items ?? []).map((item) => ({ label: item.name ?? '—', value: item.id ?? '' }))
}

/** Spanish-locale number with thousands separators, or `—` for missing or `NaN` values. */
export function formatNumber(value?: number | null): string {
  if (value == null || Number.isNaN(value)) return '—'
  return numberFormatter.format(value)
}

/** Localized size in B, whole KB or one-decimal MB (1024-based); `—` for negative or NaN input. */
export function formatFileSize(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes < 0) return '—'
  if (bytes < 1024) return i18n.global.t('common.fileSize.bytes', { value: formatNumber(bytes) })

  const kilobytes = Math.round(bytes / 1024)
  if (kilobytes < 1024) {
    return i18n.global.t('common.fileSize.kilobytes', { value: formatNumber(kilobytes) })
  }

  return i18n.global.t('common.fileSize.megabytes', {
    value: megabyteFormatter.format(bytes / (1024 * 1024)),
  })
}

/** Rounds a percentage change and prefixes positives with `+` (e.g. `+12%`, `-3%`, `0%`). */
export function formatSignedPercent(value: number): string {
  if (!Number.isFinite(value)) return '—'
  const rounded = Math.round(value)
  const sign = rounded > 0 ? '+' : ''
  return `${sign}${rounded}%`
}

/**
 * Chart axis label for a date bucket: short month and year when `granularity` is `'month'`,
 * day and month otherwise. Unparseable input is returned unchanged.
 */
export function formatBucketLabel(iso: string, granularity: string): string {
  const date = parseDateOnly(iso)
  if (!date) return iso
  return granularity === 'month' ? monthYearFormatter.format(date) : dayMonthFormatter.format(date)
}

function parseDisplayDate(value: string): Date {
  if (DATE_ONLY.test(value)) return parseDateOnly(value) ?? new Date(value)
  return new Date(value)
}

/** Medium date plus short time for an ISO timestamp, or `—` when missing or invalid. */
export function formatDateTime(value?: string | null): string {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '—'
  return dateTimeFormatter.format(date)
}

/** Medium date, or `—`. Plain `YYYY-MM-DD` values are read as local dates so they don't shift. */
export function formatDate(value?: string | null): string {
  if (!value) return '—'
  const date = parseDisplayDate(value)
  if (Number.isNaN(date.getTime())) return '—'
  return dateFormatter.format(date)
}

/** UTC `YYYY-MM-DD` value for date inputs, or `''` when missing or invalid. */
export function toDateInput(value?: string | null): string {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  return date.toISOString().slice(0, 10)
}

function toDate(value?: Date | string | null): Date | null {
  if (value == null) return null
  const date = value instanceof Date ? value : new Date(value)
  return Number.isNaN(date.getTime()) ? null : date
}

function isSameDay(a: Date, b: Date): boolean {
  return (
    a.getFullYear() === b.getFullYear() &&
    a.getMonth() === b.getMonth() &&
    a.getDate() === b.getDate()
  )
}

/**
 * `start – end` with date and time; the end shows only its time when both fall on the same day.
 * Returns just the start without an end, and `—` without a valid start.
 */
export function formatDateTimeRange(
  start?: Date | string | null,
  end?: Date | string | null,
): string {
  const startDate = toDate(start)
  if (!startDate) return '—'
  const startText = dateTimeFormatter.format(startDate)
  const endDate = toDate(end)
  if (!endDate) return startText
  const endText = isSameDay(startDate, endDate)
    ? timeFormatter.format(endDate)
    : dateTimeFormatter.format(endDate)
  return `${startText} ${RANGE_SEPARATOR} ${endText}`
}

/** Compact medium date range; a single date when the end is missing or not after the start. */
export function formatDateRange(start?: string | null, end?: string | null): string {
  if (!start) return '—'
  const startDate = parseDisplayDate(start)
  if (Number.isNaN(startDate.getTime())) return '—'
  const endDate = end ? parseDisplayDate(end) : null
  if (!endDate || Number.isNaN(endDate.getTime()) || endDate <= startDate) {
    return dateFormatter.format(startDate)
  }
  return dateFormatter.formatRange(startDate, endDate)
}

/**
 * `start – end` as times, for schedules grouped by day. A day and month is added to the start when
 * it differs from `referenceDay`, and to the end when it falls on another day than the start.
 */
export function formatTimeRange(
  start?: Date | string | null,
  end?: Date | string | null,
  referenceDay?: Date | string | null,
): string {
  const startDate = toDate(start)
  if (!startDate) return '—'
  const reference = toDate(referenceDay)
  const startText =
    reference && !isSameDay(startDate, reference)
      ? `${dayMonthFormatter.format(startDate)}, ${timeFormatter.format(startDate)}`
      : timeFormatter.format(startDate)
  const endDate = toDate(end)
  if (!endDate) return startText
  const endText = isSameDay(startDate, endDate)
    ? timeFormatter.format(endDate)
    : `${dayMonthFormatter.format(endDate)}, ${timeFormatter.format(endDate)}`
  return `${startText} ${RANGE_SEPARATOR} ${endText}`
}

/**
 * Normalizes a Spanish DNI or NIE the way the API stores it: uppercase, without spaces or hyphens.
 * Two entries that differ only in those details are the same identifier.
 */
export function normalizeNationalId(value: string): string {
  return value.replace(/[\s-]/g, '').toUpperCase()
}

/**
 * Checks a Spanish DNI (eight digits and a control letter) or NIE (X, Y or Z, seven digits and a
 * control letter) after normalizing it, with the Ministry of the Interior algorithm the API also
 * applies: the number, reading a NIE's X, Y or Z as 0, 1 or 2, modulo 23 picks the control letter.
 * Any single mistyped digit or swap of two adjacent digits changes that letter, so the value never
 * needs to be typed twice. Returns `'format'` when the value is not shaped like a DNI or NIE,
 * `'letter'` when its control letter does not match the number, and `null` when it is valid.
 */
export function nationalIdError(value: string): 'format' | 'letter' | null {
  const normalized = normalizeNationalId(value)
  if (!NATIONAL_ID_SHAPE.test(normalized)) return 'format'
  const first = normalized.charAt(0)
  const digits = `${NIE_PREFIXES[first] ?? first}${normalized.slice(1, 8)}`
  const expected = NATIONAL_ID_CONTROL_LETTERS.charAt(
    Number(digits) % NATIONAL_ID_CONTROL_LETTERS.length,
  )
  return normalized.charAt(8) === expected ? null : 'letter'
}

/** Shorthand for `nationalIdError(value) === null`, for forms that only gate their submit on it. */
export function isValidNationalId(value: string): boolean {
  return nationalIdError(value) === null
}
