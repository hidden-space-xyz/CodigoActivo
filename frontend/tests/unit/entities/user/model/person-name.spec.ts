import { describe, expect, it } from 'vitest'

import { fullName } from '@/entities/user'

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
