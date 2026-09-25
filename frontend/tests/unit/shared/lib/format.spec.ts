import { describe, expect, it, vi } from 'vitest'

import {
  ageFrom,
  formatBucketLabel,
  formatDate,
  formatDateRange,
  formatDateTime,
  formatDateTimeRange,
  formatFileSize,
  formatNumber,
  formatSignedPercent,
  formatTimeRange,
  fullName,
  isValidNationalId,
  nationalIdError,
  normalizeNationalId,
  parseDateOnly,
  toDateInput,
  toDateOnly,
  todayIso,
  toSelectOptions,
  yearsAgoIso,
} from '@/shared/lib'

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

describe('todayIso and yearsAgoIso', () => {
  it('use the current UTC date', () => {
    freezeTime(new Date('2026-09-17T12:00:00Z'))

    expect(todayIso()).toBe('2026-09-17')
    expect(yearsAgoIso(18)).toBe('2008-09-17')
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

describe('fullName', () => {
  it('joins first and last names', () => {
    expect(fullName({ firstName: 'Ada', lastName: 'Lovelace' })).toBe('Ada Lovelace')
    expect(fullName({ firstName: 'Ada', lastName: null })).toBe('Ada')
    expect(fullName({ lastName: 'Lovelace' })).toBe('Lovelace')
  })

  it('returns a dash when both names are empty', () => {
    expect(fullName({})).toBe('—')
    expect(fullName({ firstName: ' ', lastName: '' })).toBe('—')
  })
})

describe('toSelectOptions', () => {
  it('maps catalog items to options with fallbacks', () => {
    expect(
      toSelectOptions([
        { id: 'a', name: 'Alpha' },
        { id: null, name: null },
      ]),
    ).toEqual([
      { label: 'Alpha', value: 'a' },
      { label: '—', value: '' },
    ])
  })

  it('returns an empty list for missing items', () => {
    expect(toSelectOptions(null)).toEqual([])
    expect(toSelectOptions()).toEqual([])
  })
})

describe('formatNumber', () => {
  it('uses Spanish thousands separators', () => {
    expect(formatNumber(1234567)).toBe('1.234.567')
    expect(formatNumber(0)).toBe('0')
  })

  it('returns a dash for missing or NaN values', () => {
    expect(formatNumber(null)).toBe('—')
    expect(formatNumber(undefined)).toBe('—')
    expect(formatNumber(Number.NaN)).toBe('—')
  })
})

describe('formatFileSize', () => {
  it('formats bytes, kilobytes and megabytes', () => {
    expect(formatFileSize(0)).toBe('0 B')
    expect(formatFileSize(1023)).toBe('1023 B')
    expect(formatFileSize(1536)).toBe('2 KB')
    expect(formatFileSize(1024 * 1023)).toBe('1023 KB')
    expect(formatFileSize(1024 * 1024 * 2.5)).toBe('2,5 MB')
  })

  it('returns a dash for negative or non-finite sizes', () => {
    expect(formatFileSize(-1)).toBe('—')
    expect(formatFileSize(Number.NaN)).toBe('—')
    expect(formatFileSize(Number.POSITIVE_INFINITY)).toBe('—')
  })
})

describe('formatSignedPercent', () => {
  it('rounds and prefixes positive values', () => {
    expect(formatSignedPercent(12.4)).toBe('+12%')
    expect(formatSignedPercent(-3.2)).toBe('-3%')
    expect(formatSignedPercent(0.2)).toBe('0%')
  })

  it('returns a dash for non-finite values', () => {
    expect(formatSignedPercent(Number.NaN)).toBe('—')
  })
})

describe('formatBucketLabel', () => {
  it('shows month and year for monthly buckets and day and month otherwise', () => {
    expect(formatBucketLabel('2025-01-15', 'month')).toBe('ene 25')
    expect(formatBucketLabel('2025-01-15', 'day')).toBe('15 ene')
  })

  it('returns unparseable input unchanged', () => {
    expect(formatBucketLabel('week-3', 'week')).toBe('week-3')
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

describe('toDateInput', () => {
  it('returns the UTC date part', () => {
    expect(toDateInput('2025-06-01T12:00:00Z')).toBe('2025-06-01')
  })

  it('returns an empty string for missing or invalid values', () => {
    expect(toDateInput(null)).toBe('')
    expect(toDateInput('bad')).toBe('')
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

describe('normalizeNationalId', () => {
  it('uppercases and drops spaces and hyphens', () => {
    expect(normalizeNationalId(' x-1234 567-l ')).toBe('X1234567L')
    expect(normalizeNationalId('12345678z')).toBe('12345678Z')
  })
})

describe('nationalIdError', () => {
  it.each(['12345678Z', '00000000T', 'X1234567L', 'Y1234567X', 'Z1234567R', ' 1234-5678 z '])(
    'accepts %s with its control letter',
    (value) => {
      expect(nationalIdError(value)).toBeNull()
    },
  )

  it.each(['12345678A', '12345687Z', 'X1234567A', 'Y1234567L'])(
    'reports a control letter that does not match %s',
    (value) => {
      expect(nationalIdError(value)).toBe('letter')
    },
  )

  it.each(['1234567Z', '123456789Z', '12345678', 'W1234567L', 'ABCDEFGHI', '12.345.678-Z', ''])(
    'reports %j as not shaped like a DNI or NIE',
    (value) => {
      expect(nationalIdError(value)).toBe('format')
    },
  )

  it('flags every single mistyped digit and every swap of two adjacent digits', () => {
    const valid = '12345678Z'
    const typos: string[] = []
    for (let index = 0; index < 8; index += 1) {
      for (const digit of '0123456789') {
        if (digit !== valid.charAt(index)) {
          typos.push(`${valid.slice(0, index)}${digit}${valid.slice(index + 1)}`)
        }
      }
      if (index < 7) {
        typos.push(
          `${valid.slice(0, index)}${valid.charAt(index + 1)}${valid.charAt(index)}${valid.slice(index + 2)}`,
        )
      }
    }

    expect(typos).toHaveLength(79)
    expect(typos.filter((typo) => nationalIdError(typo) !== 'letter')).toEqual([])
  })
})

describe('isValidNationalId', () => {
  it('accepts only values without a problem', () => {
    expect(isValidNationalId('x-1234567-l')).toBe(true)
    expect(isValidNationalId('X1234567A')).toBe(false)
    expect(isValidNationalId('X123')).toBe(false)
  })
})
