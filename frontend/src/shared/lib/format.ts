const dateTimeFormatter = new Intl.DateTimeFormat('es-ES', {
  dateStyle: 'medium',
  timeStyle: 'short',
})

const dateFormatter = new Intl.DateTimeFormat('es-ES', { dateStyle: 'medium' })

const DATE_ONLY = /^\d{4}-\d{2}-\d{2}$/

/** Parses a date-only string (yyyy-MM-dd, or the date part of an ISO datetime) as a local Date. */
export function parseDateOnly(value?: string | null): Date | null {
  if (!value) return null
  const [year, month, day] = value.slice(0, 10).split('-').map(Number)
  if (!year || !month || !day) return null
  return new Date(year, month - 1, day)
}

/** Formats a local Date as a date-only string (yyyy-MM-dd) using its local calendar day. */
export function toDateOnly(date: Date): string {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

/** Today's date as a yyyy-MM-dd string (UTC-based, matching backend date comparisons). */
export function todayIso(): string {
  return new Date().toISOString().slice(0, 10)
}

/** The yyyy-MM-dd date exactly `years` years ago (UTC-based). */
export function yearsAgoIso(years: number): string {
  const date = new Date()
  date.setFullYear(date.getFullYear() - years)
  return date.toISOString().slice(0, 10)
}

/** Age in whole years for a birth date, or null when the value is missing or invalid. */
export function ageFrom(value?: Date | string | null): number | null {
  if (!value) return null
  const birth = value instanceof Date ? value : new Date(value)
  if (Number.isNaN(birth.getTime())) return null
  const now = new Date()
  let age = now.getFullYear() - birth.getFullYear()
  const monthDiff = now.getMonth() - birth.getMonth()
  if (monthDiff < 0 || (monthDiff === 0 && now.getDate() < birth.getDate())) age--
  return age
}

function parseDisplayDate(value: string): Date {
  if (DATE_ONLY.test(value)) return parseDateOnly(value) ?? new Date(value)
  return new Date(value)
}

export function formatDateTime(value?: string | null): string {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '—'
  return dateTimeFormatter.format(date)
}

export function formatDate(value?: string | null): string {
  if (!value) return '—'
  const date = parseDisplayDate(value)
  if (Number.isNaN(date.getTime())) return '—'
  return dateFormatter.format(date)
}

export function toDateInput(value?: string | null): string {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  return date.toISOString().slice(0, 10)
}
