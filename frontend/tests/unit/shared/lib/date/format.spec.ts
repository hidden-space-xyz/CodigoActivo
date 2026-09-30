import { describe, expect, it, vi } from 'vitest'

import {
  ageFrom,
  formatDate,
  formatDateRange,
  formatDateTime,
  formatDateTimeRange,
  formatTimeRange,
  parseDateOnly,
  toDateOnly,
  todayIso,
} from '@/shared/lib/date'

function freezeTime(date: Date): void {
  vi.useFakeTimers({ toFake: ['Date'] })
  vi.setSystemTime(date)
}

describe('parseDateOnly', () => {
  it('parses the date prefix as local midnight', () => {
    expect(parseDateOnly('2025-03-09T23:59:00Z')).toEqual(new Date(2025, 2, 9))
    expect(parseDateOnly('2024-12-31')).toEqual(new Date(2024, 11, 31))
  })

  it('returns null for empty or malformed values', () => {
    expect(parseDateOnly(undefined)).toBeNull()
    expect(parseDateOnly(null)).toBeNull()
    expect(parseDateOnly('')).toBeNull()
    expect(parseDateOnly('not-a-date')).toBeNull()
    expect(parseDateOnly('2025-00-10')).toBeNull()
  })
})

describe('toDateOnly', () => {
  it('formats the local calendar date with zero padding', () => {
    expect(toDateOnly(new Date(2025, 0, 5, 23, 30))).toBe('2025-01-05')
  })
})

describe('todayIso', () => {
  it('uses the current UTC date', () => {
    freezeTime(new Date('2026-09-17T12:00:00Z'))

    expect(todayIso()).toBe('2026-09-17')
  })
})

describe('ageFrom', () => {
  it('counts completed years for strings and dates', () => {
    freezeTime(new Date(2026, 8, 17, 12))

    expect(ageFrom('2000-09-17')).toBe(26)
    expect(ageFrom('2000-09-18')).toBe(25)
    expect(ageFrom('2000-10-01')).toBe(25)
    expect(ageFrom('2000-08-30')).toBe(26)
    expect(ageFrom(new Date(1990, 0, 1))).toBe(36)
  })

  it('returns null for missing or invalid dates', () => {
    expect(ageFrom(null)).toBeNull()
    expect(ageFrom(undefined)).toBeNull()
    expect(ageFrom('garbage')).toBeNull()
    expect(ageFrom(new Date('invalid'))).toBeNull()
  })
})

describe('formatDateTime', () => {
  it('formats a timestamp with medium date and short time', () => {
    expect(formatDateTime('2025-01-15T10:30:00')).toBe('15 ene 2025, 10:30')
  })

  it('returns a dash for missing or invalid values', () => {
    expect(formatDateTime(null)).toBe('—')
    expect(formatDateTime('')).toBe('—')
    expect(formatDateTime('nope')).toBe('—')
  })
})

describe('formatDate', () => {
  it('reads date-only values as local dates and formats timestamps', () => {
    expect(formatDate('2025-01-15')).toBe('15 ene 2025')
    expect(formatDate('2025-01-15T10:30:00')).toBe('15 ene 2025')
  })

  it('returns a dash for missing or invalid values', () => {
    expect(formatDate(undefined)).toBe('—')
    expect(formatDate('2025-00-00')).toBe('—')
    expect(formatDate('invalid')).toBe('—')
  })
})

describe('formatDateTimeRange', () => {
  const start = new Date(2025, 0, 15, 10, 30)

  it('shows only the end time when both fall on the same day', () => {
    expect(formatDateTimeRange(start, '2025-01-15T12:00:00')).toBe('15 ene 2025, 10:30 – 12:00')
  })

  it('shows the full end when it falls on another day', () => {
    expect(formatDateTimeRange('2025-01-15T10:30:00', new Date(2025, 0, 20, 18, 5))).toBe(
      '15 ene 2025, 10:30 – 20 ene 2025, 18:05',
    )
  })

  it('returns just the start without a valid end', () => {
    expect(formatDateTimeRange(start)).toBe('15 ene 2025, 10:30')
    expect(formatDateTimeRange(start, 'invalid')).toBe('15 ene 2025, 10:30')
  })

  it('returns a dash without a valid start', () => {
    expect(formatDateTimeRange(null, start)).toBe('—')
    expect(formatDateTimeRange('invalid')).toBe('—')
  })
})

describe('formatDateRange', () => {
  it('formats a compact range when the end is after the start', () => {
    expect(formatDateRange('2025-01-15', '2025-01-20')).toBe('15–20 ene 2025')
  })

  it('shows a single date when the end is missing, invalid or not after the start', () => {
    expect(formatDateRange('2025-01-15')).toBe('15 ene 2025')
    expect(formatDateRange('2025-01-15', null)).toBe('15 ene 2025')
    expect(formatDateRange('2025-01-15', 'bad')).toBe('15 ene 2025')
    expect(formatDateRange('2025-01-15', '2025-01-15')).toBe('15 ene 2025')
    expect(formatDateRange('2025-01-15', '2025-01-10')).toBe('15 ene 2025')
  })

  it('returns a dash without a valid start', () => {
    expect(formatDateRange(null, '2025-01-20')).toBe('—')
    expect(formatDateRange('bad')).toBe('—')
  })
})

describe('formatTimeRange', () => {
  const start = new Date(2025, 0, 15, 10, 30)

  it('shows times only on the reference day', () => {
    expect(formatTimeRange(start, new Date(2025, 0, 15, 12, 0), '2025-01-15T00:00:00')).toBe(
      '10:30 – 12:00',
    )
  })

  it('adds the day to a start outside the reference day and to an end on another day', () => {
    expect(formatTimeRange(start, new Date(2025, 0, 16, 9, 0), new Date(2025, 0, 14))).toBe(
      '15 ene, 10:30 – 16 ene, 9:00',
    )
  })

  it('returns only the start without an end and a dash without a start', () => {
    expect(formatTimeRange(start)).toBe('10:30')
    expect(formatTimeRange(undefined, start)).toBe('—')
  })
})
