import { describe, expect, it, vi } from 'vitest'

import { useTwoFactor } from '@/pages/account/model/use-two-factor'

import { mountComposable, t } from '../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../support/server'

describe('useTwoFactor', () => {
  it('reads the method from the session, defaulting to email for guests', async () => {
    const { result } = await mountComposable(() => useTwoFactor())
    expect(result.isAuthenticator.value).toBe(false)
    expect(result.email.value).toBe('')

    const authenticator = await mountComposable(() => useTwoFactor(), {
      user: { twoFactorMethod: 'Authenticator', email: 'ada@example.test' },
    })
    expect(authenticator.result.isAuthenticator.value).toBe(true)
    expect(authenticator.result.email.value).toBe('ada@example.test')
  })

  it('keeps a refused setup code request until the next one is emailed', async () => {
    const bodies: unknown[] = []
    server.use(
      http.post('/api/me/confirmation-code', async ({ request }) => {
        bodies.push(await request.json())
        return bodies.length === 1
          ? apiError(409, 'TwoFactorResendCooldownActive')
          : new HttpResponse(null, { status: 204 })
      }),
    )
    const { result } = await mountComposable(() => useTwoFactor(), { user: {} })

    result.requestSetupCode.mutate({ currentPassword: 'secret', purpose: 'AuthenticatorSetup' })
    await vi.waitFor(() =>
      expect(result.errorMessage.value).toBe(t('errors.TwoFactorResendCooldownActive')),
    )

    result.requestSetupCode.mutate({ currentPassword: 'secret', purpose: 'AuthenticatorSetup' })
    await vi.waitFor(() => expect(result.errorMessage.value).toBe(''))
    expect(bodies).toEqual([
      { currentPassword: 'secret', purpose: 'AuthenticatorSetup' },
      { currentPassword: 'secret', purpose: 'AuthenticatorSetup' },
    ])
  })
})
