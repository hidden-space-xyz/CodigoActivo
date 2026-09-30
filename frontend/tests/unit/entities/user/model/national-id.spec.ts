import { describe, expect, it } from 'vitest'

import { nationalIdError, normalizeNationalId } from '@/entities/user/model/national-id'

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
