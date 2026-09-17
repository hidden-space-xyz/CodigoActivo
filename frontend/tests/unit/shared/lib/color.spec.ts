import { describe, expect, it } from 'vitest'

import { hexLuminance, normalizeHexColor } from '@/shared/lib'

describe('hexLuminance', () => {
  it('returns 0 for black, 1 for white and weights green highest', () => {
    expect(hexLuminance('#000000')).toBe(0)
    expect(hexLuminance('#ffffff')).toBeCloseTo(1)
    expect(hexLuminance('#00ff00')).toBeGreaterThan(hexLuminance('#ff0000'))
    expect(hexLuminance('#ff0000')).toBeGreaterThan(hexLuminance('#0000ff'))
  })
})

describe('normalizeHexColor', () => {
  it('trims and keeps six-digit colors', () => {
    expect(normalizeHexColor('  #A1b2C3 ')).toBe('#A1b2C3')
  })

  it('expands three-digit colors', () => {
    expect(normalizeHexColor('#aB3')).toBe('#aaBB33')
  })

  it('returns null for empty or invalid input', () => {
    expect(normalizeHexColor(null)).toBeNull()
    expect(normalizeHexColor(undefined)).toBeNull()
    expect(normalizeHexColor('')).toBeNull()
    expect(normalizeHexColor('red')).toBeNull()
    expect(normalizeHexColor('#12345')).toBeNull()
    expect(normalizeHexColor('#ggg')).toBeNull()
  })
})
