import { describe, expect, it } from 'vitest'

import { sessionQueryKeys } from '@/entities/session'

describe('sessionQueryKeys', () => {
  it('nests the login challenge key under the shared root', () => {
    expect(sessionQueryKeys.all).toEqual(['session'])
    expect(sessionQueryKeys.loginChallenge()).toEqual(['session', 'login-challenge'])
  })
})
