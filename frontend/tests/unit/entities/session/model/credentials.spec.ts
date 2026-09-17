import { describe, expect, it } from 'vitest'

import { createEmptyCredentials } from '@/entities/session'

describe('createEmptyCredentials', () => {
  it('returns a blank login form', () => {
    expect(createEmptyCredentials()).toEqual({ identifier: '', password: '' })
  })

  it('returns a new object each time so forms do not share state', () => {
    const first = createEmptyCredentials()
    first.identifier = 'ada@example.test'

    expect(createEmptyCredentials()).toEqual({ identifier: '', password: '' })
  })
})
