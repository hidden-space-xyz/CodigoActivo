import { describe, expect, it } from 'vitest'

import { signupStatusTagType } from '@/entities/activity'

describe('signupStatusTagType', () => {
  it.each([
    ['Confirmada', 'success'],
    ['CONFIRMADO', 'success'],
    ['Rechazada', 'danger'],
    ['Solicitada', 'info'],
    ['', 'info'],
  ])('tags the status %j as %s', (name, type) => {
    expect(signupStatusTagType(name)).toBe(type)
  })
})
