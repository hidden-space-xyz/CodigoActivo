import { describe, expect, it } from 'vitest'

import { toAuthUser, toLoginChallenge } from '@/entities/session/api/mapper'
import { buildUserResponse } from '../../../../support/builders'

describe('toAuthUser', () => {
  it('maps the signed-in user', () => {
    expect(toAuthUser(buildUserResponse({ isAdmin: true }))).toEqual({
      id: 'user-1',
      firstName: 'Ada',
      lastName: 'Lovelace',
      email: 'ada@example.test',
      phone: '600000000',
      isAdmin: true,
      earlySignupEligible: false,
      twoFactorMethod: 'Email',
    })
  })

  it('keeps the early signup entitlement the API decided', () => {
    expect(toAuthUser(buildUserResponse({ earlySignupEligible: true })).earlySignupEligible).toBe(
      true,
    )
  })

  it('shows missing contact details as empty text', () => {
    expect(toAuthUser(buildUserResponse({ email: null, phone: null }))).toMatchObject({
      email: '',
      phone: '',
    })
  })
})

describe('toLoginChallenge', () => {
  it('maps the method and the masked address', () => {
    expect(toLoginChallenge({ method: 'Authenticator', maskedEmail: null })).toEqual({
      method: 'Authenticator',
      maskedEmail: null,
    })
    expect(toLoginChallenge({ method: 'Email', maskedEmail: 'a***@example.test' })).toEqual({
      method: 'Email',
      maskedEmail: 'a***@example.test',
    })
  })

  it('reads a missing masked address as none', () => {
    expect(toLoginChallenge({ method: 'Email' })).toEqual({ method: 'Email', maskedEmail: null })
  })
})
