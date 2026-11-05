import { describe, expect, it } from 'vitest'

import { toAuthUser, toLoginChallenge } from '@/entities/session/api/mapper'
import { buildUserResponse } from '../../../../support/builders'

describe('toAuthUser', () => {
  it('maps the signed-in user, the initial account as the superadministrator', () => {
    expect(
      toAuthUser(
        buildUserResponse({
          staffRole: 'Administrator',
          isInitialAdmin: true,
          isPrivacyOfficer: true,
        }),
      ),
    ).toEqual({
      id: 'user-1',
      firstName: 'Ada',
      lastName: 'Lovelace',
      email: 'ada@example.test',
      phone: '600000000',
      staffRole: 'Administrator',
      isSuperAdmin: true,
      isPrivacyOfficer: true,
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
  it('reads a missing masked address as none', () => {
    expect(toLoginChallenge({ method: 'Email' })).toEqual({ method: 'Email', maskedEmail: null })
  })
})
