const dateTimeFormatter = new Intl.DateTimeFormat('es-ES', {
  dateStyle: 'medium',
  timeStyle: 'short',
})

const dateFormatter = new Intl.DateTimeFormat('es-ES', { dateStyle: 'medium' })

const timeFormatter = new Intl.DateTimeFormat('es-ES', { timeStyle: 'short' })

const dayMonthFormatter = new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'short' })

const RANGE_SEPARATOR = '–'

const DATE_ONLY = /^\d{4}-\d{2}-\d{2}$/

export function parseDateOnly(value?: string | null): Date | null {
  if (!value) return null
  const [year, month, day] = value.slice(0, 10).split('-').map(Number)
  if (!year || !month || !day) return null
  return new Date(year, month - 1, day)
}

export function toDateOnly(date: Date): string {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

export function todayIso(): string {
  return new Date().toISOString().slice(0, 10)
}

export function yearsAgoIso(years: number): string {
  const date = new Date()
  date.setFullYear(date.getFullYear() - years)
  return date.toISOString().slice(0, 10)
}

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
