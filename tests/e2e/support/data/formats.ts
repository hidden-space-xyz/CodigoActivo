import { TIME_ZONE } from '../config/environment'

const LOCALE = 'es-ES'
const RANGE_SEPARATOR = '–'

const DATE_ONLY = /^\d{4}-\d{2}-\d{2}$/

const dayFormat = new Intl.DateTimeFormat(LOCALE, { dateStyle: 'medium', timeZone: 'UTC' })
const localDayFormat = new Intl.DateTimeFormat(LOCALE, {
  dateStyle: 'medium',
  timeZone: TIME_ZONE,
})
const momentFormat = new Intl.DateTimeFormat(LOCALE, {
  dateStyle: 'medium',
  timeStyle: 'short',
  timeZone: TIME_ZONE,
})
const timeFormat = new Intl.DateTimeFormat(LOCALE, { timeStyle: 'short', timeZone: TIME_ZONE })
const calendarDay = new Intl.DateTimeFormat('en-CA', { dateStyle: 'short', timeZone: TIME_ZONE })

function utcDay(day: string): Date {
  return new Date(`${day.slice(0, 10)}T00:00:00Z`)
}

/**
 * A `YYYY-MM-DD` day, or the day of an instant in the application time zone, as the site writes
 * it, such as «4 nov 2026».
 */
export function dayText(value: string): string {
  return DATE_ONLY.test(value)
    ? dayFormat.format(utcDay(value))
    : localDayFormat.format(new Date(value))
}

/** The days from `start` to `end` as cards and event pages write them, such as «5–7 nov 2026». */
export function dayRangeText(start: string, end: string): string {
  return end.slice(0, 10) > start.slice(0, 10)
    ? dayFormat.formatRange(utcDay(start), utcDay(end))
    : dayText(start)
}

/** What the date line of a card reads, such as «Fecha: 4 nov 2026», whatever its spacing. */
export function cardDate(label: string, value: string): RegExp {
  const pattern = (text: string) =>
    text
      .split(/\s+/)
      .map((word) => word.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'))
      .join('\\s+')
  return new RegExp(`^${pattern(label)}:\\s*${pattern(value)}$`)
}

/**
 * An instant as the site writes it in the application time zone, such as «4 nov 2026, 9:20», or
 * «—» when there is none.
 */
export function momentText(instant: string | null | undefined): string {
  return instant ? momentFormat.format(new Date(instant)) : '—'
}

/** Two instants as the event page writes a signup window; the end drops the date on the same day. */
export function momentRangeText(start: string, end: string): string {
  const sameDay = calendarDay.format(new Date(start)) === calendarDay.format(new Date(end))
  const endText = sameDay ? timeFormat.format(new Date(end)) : momentText(end)
  return `${momentText(start)} ${RANGE_SEPARATOR} ${endText}`
}

/** The times of an activity that starts and ends on the same day, such as «10:00 – 12:00». */
export function timeRangeText(start: string, end: string): string {
  return `${timeFormat.format(new Date(start))} ${RANGE_SEPARATOR} ${timeFormat.format(new Date(end))}`
}

function twoDigits(value: number): string {
  return String(value).padStart(2, '0')
}

/** A `YYYY-MM-DD` day as the Element Plus date pickers of the admin forms take it, `DD/MM/YYYY`. */
export function pickerDate(day: string): string {
  const [year, month, date] = day.split('-')
  return `${date}/${month}/${year}`
}

/** The wall-clock time `hour:minute` of `day` as the datetime pickers take it, `DD/MM/YYYY HH:mm`. */
export function pickerMoment(day: string, hour: number, minute = 0): string {
  return `${pickerDate(day)} ${twoDigits(hour)}:${twoDigits(minute)}`
}
