import { describe, expect, it } from 'vitest'

import { toAuthUser, toLoginChallenge } from '@/entities/session/api/mapper'
import { EARLY_SIGNUP_USER_TYPE_IDS } from '@/shared/config'

import { buildUserResponse } from '../../../../support/fixtures/user'

describe('toAuthUser', () => {
  it('maps the signed-in user and marks regular user types as not early-signup eligible', () => {
    expect(toAuthUser(buildUserResponse({ isAdmin: true }))).toEqual({
      id: 'user-1',
      firstName: 'Ada',
      lastName: 'Lovelace',
      email: 'ada@example.test',
      phone: '600000000',
      birthDate: '1990-05-10',
      isAdmin: true,
      userTypeId: 'type-participant',
      earlySignupEligible: false,
      twoFactorMethod: 'Email',
    })
  })

  it.each(EARLY_SIGNUP_USER_TYPE_IDS)('marks user type %s as early-signup eligible', (typeId) => {
    const user = toAuthUser(buildUserResponse({ type: { id: typeId, name: 'Socio' } }))

    expect(user.userTypeId).toBe(typeId)
    expect(user.earlySignupEligible).toBe(true)
  })

  it('defaults a bare response to an anonymous-looking user without type', () => {
    expect(toAuthUser({})).toEqual({
      id: '',
      firstName: '',
      lastName: '',
      email: '',
      phone: '',
      birthDate: '',
      isAdmin: false,
      userTypeId: '',
      earlySignupEligible: false,
      twoFactorMethod: 'Email',
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

  it('defaults a bare response to an email challenge without address', () => {
    expect(toLoginChallenge({})).toEqual({ method: 'Email', maskedEmail: null })
  })
})
