import { describe, expect, it } from 'vitest'

import { toAuthUser } from '@/entities/session/api/mapper'
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
    })
  })
})
