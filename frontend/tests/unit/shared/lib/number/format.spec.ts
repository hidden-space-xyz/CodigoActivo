import { describe, expect, it } from 'vitest'

import { formatFileSize, formatNumber, formatSignedPercent } from '@/shared/lib/number'

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
