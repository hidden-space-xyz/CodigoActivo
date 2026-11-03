import { describe, expect, it, vi } from 'vitest'

import { useDeleteAccount } from '@/pages/account/model/use-delete-account'

import { t } from '../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../support/server'
import { mountComposable } from '../../../../support/render'
import { sessionOf } from '../../../../support/session'

describe('useDeleteAccount', () => {
  it('reads the second factor and the address from the session, defaulting to email', async () => {
    const guest = await mountComposable(() => useDeleteAccount())
    expect(guest.result.isAuthenticator.value).toBe(false)
    expect(guest.result.email.value).toBe('')

    const authenticator = await mountComposable(() => useDeleteAccount(), {
      user: { twoFactorMethod: 'Authenticator', email: 'ada@example.test' },
    })
    expect(authenticator.result.isAuthenticator.value).toBe(true)
    expect(authenticator.result.email.value).toBe('ada@example.test')
  })

  it('keeps the cooldown error when the code cannot be sent again', async () => {
    server.use(
      http.post('/api/me/deletion/code', () => apiError(409, 'TwoFactorResendCooldownActive')),
    )
    const { result } = await mountComposable(() => useDeleteAccount(), { user: {} })

    result.requestCode.mutate('secret')

    await vi.waitFor(() =>
      expect(result.errorMessage.value).toBe(t('errors.TwoFactorResendCooldownActive')),
    )
    result.reset()
    expect(result.errorMessage.value).toBe('')
  })

  it('still clears the session and leaves when signing out fails afterwards', async () => {
    server.use(
      http.post('/api/me/deletion', () => new HttpResponse(null, { status: 204 })),
      http.post('/api/auth/logout', () => apiError(500)),
    )
    const { result, router, queryClient } = await mountComposable(() => useDeleteAccount(), {
      user: {},
      route: '/account',
    })

    await result.confirm.mutateAsync({ currentPassword: 'secret', code: '123456' })

    expect(sessionOf(queryClient).user).toBeNull()
    expect(router.currentRoute.value.name).toBe('home')
  })

  it('explains a refusal of the initial administrator', async () => {
    server.use(
      http.get('/api/me/deletion', () => HttpResponse.json({ allowed: true })),
      http.post('/api/me/deletion', () => apiError(403, 'UserDeleteInitialAdminForbidden')),
    )
    const { result } = await mountComposable(() => useDeleteAccount(), { user: { isAdmin: true } })

    result.confirm.mutate({ currentPassword: 'secret', code: '123456' })

    await vi.waitFor(() =>
      expect(result.errorMessage.value).toBe(t('errors.UserDeleteInitialAdminForbidden')),
    )
  })
})
