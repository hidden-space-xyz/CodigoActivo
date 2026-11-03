import { describe, expect, it } from 'vitest'

import { useTwoFactor } from '@/pages/account/model/use-two-factor'

import { mountComposable } from '../../../../support/render'

describe('useTwoFactor', () => {
  it('reads the method from the session, defaulting to email for guests', async () => {
    const { result } = await mountComposable(() => useTwoFactor())
    expect(result.isAuthenticator.value).toBe(false)

    const authenticator = await mountComposable(() => useTwoFactor(), {
      user: { twoFactorMethod: 'Authenticator' },
    })
    expect(authenticator.result.isAuthenticator.value).toBe(true)
  })
})
