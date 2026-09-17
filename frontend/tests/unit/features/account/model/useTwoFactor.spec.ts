import { describe, expect, it, vi } from 'vitest'

import { accountQueryKeys } from '@/entities/account'
import { useSession } from '@/entities/session'
import { useTwoFactor } from '@/features/account/model/useTwoFactor'

import { buildUserResponse } from '../../../../support/fixtures/user'
import { t } from '../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../support/server'

import { withSetup } from './with-setup'

const SETUP = {
  sharedKey: 'JBSW Y3DP EHPK 3PXP',
  authenticatorUri: 'otpauth://totp/x?secret=JBSWY3DPEHPK3PXP',
}

function serveMe(user = buildUserResponse({ twoFactorMethod: 'Authenticator' })) {
  const calls = { count: 0 }
  server.use(
    http.get('/api/auth/me', () => {
      calls.count += 1
      return HttpResponse.json(user)
    }),
  )
  return calls
}

function notificationTexts(): string[] {
  return [...document.body.querySelectorAll('.el-notification')].map((n) => n.textContent)
}

describe('useTwoFactor', () => {
  it('reads the method from the session, defaulting to email for guests', async () => {
    const { result } = await withSetup(() => useTwoFactor())
    expect(result.method.value).toBe('Email')
    expect(result.isAuthenticator.value).toBe(false)

    const authenticator = await withSetup(() => useTwoFactor(), {
      user: { twoFactorMethod: 'Authenticator' },
    })
    expect(authenticator.result.method.value).toBe('Authenticator')
    expect(authenticator.result.isAuthenticator.value).toBe(true)
  })

  it('starts the enrollment with the password and renders the QR code', async () => {
    let body: unknown
    server.use(
      http.post('/api/auth/two-factor/authenticator/setup', async ({ request }) => {
        body = await request.json()
        return HttpResponse.json(SETUP)
      }),
    )
    const { result } = await withSetup(() => useTwoFactor(), { user: {} })

    result.beginSetup.mutate('secret')
    await vi.waitFor(() => expect(result.qrCodeUrl.value).not.toBeNull())

    expect(body).toEqual({ currentPassword: 'secret' })
    expect(result.setup.value).toEqual(SETUP)
    expect(result.qrCodeUrl.value?.startsWith('data:image/svg+xml')).toBe(true)
    expect(result.errorMessage.value).toBe('')

    result.reset()
    expect(result.setup.value).toBeNull()
    expect(result.qrCodeUrl.value).toBeNull()
  })

  it('keeps the wrong-password error for the dialog', async () => {
    server.use(
      http.post('/api/auth/two-factor/authenticator/setup', () =>
        apiError(400, 'UserCurrentPasswordIncorrect'),
      ),
    )
    const { result } = await withSetup(() => useTwoFactor(), { user: {} })

    result.beginSetup.mutate('wrong')
    await vi.waitFor(() =>
      expect(result.errorMessage.value).toBe(t('errors.UserCurrentPasswordIncorrect')),
    )
    expect(result.setup.value).toBeNull()
  })

  it('confirms the enrollment, refreshes the session and the profile, and notifies', async () => {
    const me = serveMe()
    let body: unknown
    server.use(
      http.post('/api/auth/two-factor/authenticator/confirm', async ({ request }) => {
        body = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { result, queryClient } = await withSetup(() => useTwoFactor(), { user: {} })
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries')

    result.confirmSetup.mutate('123456')
    await vi.waitFor(() => expect(useSession().user?.twoFactorMethod).toBe('Authenticator'))

    expect(body).toEqual({ code: '123456' })
    expect(me.count).toBe(1)
    expect(result.isAuthenticator.value).toBe(true)
    expect(invalidate).toHaveBeenCalledWith({ queryKey: accountQueryKeys.me() })
    await vi.waitFor(() =>
      expect(notificationTexts().join()).toContain(t('features.account.twoFactor.enabledSummary')),
    )
  })

  it('reports a rejected confirmation code without touching the session', async () => {
    server.use(
      http.post('/api/auth/two-factor/authenticator/confirm', () =>
        apiError(400, 'TwoFactorCodeInvalid'),
      ),
    )
    const { result } = await withSetup(() => useTwoFactor(), { user: {} })

    result.confirmSetup.mutate('000000')
    await vi.waitFor(() => expect(result.errorMessage.value).toBe(t('errors.TwoFactorCodeInvalid')))
    expect(useSession().user?.twoFactorMethod).toBe('Email')
  })

  it('returns to email with the password and a code, then refreshes and notifies', async () => {
    serveMe(buildUserResponse({ twoFactorMethod: 'Email' }))
    let body: unknown
    server.use(
      http.post('/api/auth/two-factor/email', async ({ request }) => {
        body = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { result } = await withSetup(() => useTwoFactor(), {
      user: { twoFactorMethod: 'Authenticator' },
    })
    expect(result.isAuthenticator.value).toBe(true)

    result.disable.mutate({ currentPassword: 'secret', code: '123456' })
    await vi.waitFor(() => expect(result.isAuthenticator.value).toBe(false))

    expect(body).toEqual({ currentPassword: 'secret', code: '123456' })
    await vi.waitFor(() =>
      expect(notificationTexts().join()).toContain(t('features.account.twoFactor.disabledSummary')),
    )
  })

  it('keeps the error when the authenticator cannot be removed', async () => {
    server.use(http.post('/api/auth/two-factor/email', () => apiError(403, 'TwoFactorLocked')))
    const { result } = await withSetup(() => useTwoFactor(), {
      user: { twoFactorMethod: 'Authenticator' },
    })

    result.disable.mutate({ currentPassword: 'secret', code: '000000' })
    await vi.waitFor(() => expect(result.errorMessage.value).toBe(t('errors.TwoFactorLocked')))
    expect(result.isAuthenticator.value).toBe(true)
  })
})
