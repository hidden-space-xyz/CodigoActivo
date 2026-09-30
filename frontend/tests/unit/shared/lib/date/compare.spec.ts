import { describe, expect, it } from 'vitest'

import { isDayAfter, isDayBefore } from '@/shared/lib/date'

describe('isDayBefore and isDayAfter', () => {
  const noon = new Date(2026, 9, 10, 12, 0)

  it('compare calendar days, whatever the time', () => {
    expect(isDayBefore(noon, new Date(2026, 9, 11, 0, 0))).toBe(true)
    expect(isDayBefore(noon, new Date(2026, 9, 10, 23, 59))).toBe(false)
    expect(isDayAfter(noon, new Date(2026, 9, 9, 23, 59))).toBe(true)
    expect(isDayAfter(noon, new Date(2026, 9, 10, 0, 0))).toBe(false)
  })

  it('take the day of an ISO reference', () => {
    expect(isDayBefore(noon, '2026-10-11')).toBe(true)
    expect(isDayBefore(noon, '2026-10-10T00:00:00Z')).toBe(false)
    expect(isDayAfter(noon, '2026-10-09')).toBe(true)
  })

  it('never hold without a reference', () => {
    expect(isDayBefore(noon, null)).toBe(false)
    expect(isDayAfter(noon, undefined)).toBe(false)
    expect(isDayAfter(noon, '')).toBe(false)
  })
})
