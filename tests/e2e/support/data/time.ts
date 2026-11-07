import { TIME_ZONE } from '../config/environment'

/** The milliseconds of a day. */
export const DAY_MS = 86_400_000

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
  const { year = 0, month = 1, day = 1 } = partsOf(new Date())
  return new Date(Date.UTC(year, month - 1, day + offset)).toISOString().slice(0, 10)
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

/** The whole days from `from` to `to`, each a `YYYY-MM-DD` day or an instant. */
export function daysBetween(from: string, to: string): number {
  return Math.round((Date.parse(to) - Date.parse(from)) / DAY_MS)
}

/** The age in whole years on `day` (`YYYY-MM-DD`) of someone born on `birthDate`. */
export function ageOn(birthDate: string, day: string): number {
  const [birthYear = 0, birthMonth = 1, birthDay = 1] = birthDate.split('-').map(Number)
  const [year = 0, month = 1, date = 1] = day.slice(0, 10).split('-').map(Number)
  const hadBirthday = month > birthMonth || (month === birthMonth && date >= birthDay)
  return year - birthYear - (hadBirthday ? 0 : 1)
}

/** The birth date of someone who turns `years` years old `days` days from today. */
export function birthDateForAge(years: number, days = 0): string {
  const [year = 0, month = 1, date = 1] = dayFromToday(days).split('-').map(Number)
  const birth = new Date(Date.UTC(year - years, month - 1, date))
  return birth.toISOString().slice(0, 10)
}
