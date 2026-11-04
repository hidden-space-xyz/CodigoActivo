import { TIME_ZONE } from '../config/environment'

const DAY_MS = 86_400_000

const madridParts = new Intl.DateTimeFormat('en-CA', {
  timeZone: TIME_ZONE,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  hourCycle: 'h23',
})

function partsOf(instant: Date): Record<string, number> {
  return Object.fromEntries(
    madridParts
      .formatToParts(instant)
      .filter((part) => part.type !== 'literal')
      .map((part) => [part.type, Number(part.value)]),
  )
}

/** The calendar day `offset` days from today in the application time zone, as `YYYY-MM-DD`. */
export function dayFromToday(offset: number): string {
  const { year, month, day } = partsOf(new Date(Date.now() + offset * DAY_MS))
  return [year, month, day].map((value) => String(value).padStart(2, '0')).join('-')
}

/** The instant of the wall-clock time `hour:minute` on `day` in the application time zone. */
export function instantOn(day: string, hour: number, minute = 0): Date {
  const [year = 0, month = 1, date = 1] = day.split('-').map(Number)
  const wallClock = Date.UTC(year, month - 1, date, hour, minute)
  const shown = partsOf(new Date(wallClock))
  const shownClock = Date.UTC(
    shown.year ?? year,
    (shown.month ?? month) - 1,
    shown.day ?? date,
    shown.hour ?? hour,
    shown.minute ?? minute,
  )
  return new Date(wallClock - (shownClock - wallClock))
}

/** The instant `hours` hours from now. */
export function hoursFromNow(hours: number): Date {
  return new Date(Date.now() + hours * 3_600_000)
}

/** The birth date of someone who turns `years` years old `days` days from today. */
export function birthDateForAge(years: number, days = 0): string {
  const [year = 0, month = 1, date = 1] = dayFromToday(days).split('-').map(Number)
  const birth = new Date(Date.UTC(year - years, month - 1, date))
  return birth.toISOString().slice(0, 10)
}
