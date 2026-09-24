import { describe, expect, it, vi } from 'vitest'

import { useSession } from '@/entities/session'
import { useDeleteAccount } from '@/features/account/model/useDeleteAccount'

import { t } from '../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../support/server'

import { withSetup } from './with-setup'

function serveDeletion() {
  const calls: { url: string; body: unknown }[] = []
  server.use(
    http.post('/api/me/deletion/code', async ({ request }) => {
      calls.push({ url: '/api/me/deletion/code', body: await request.json() })
      return new HttpResponse(null, { status: 204 })
    }),
    http.post('/api/me/deletion', async ({ request }) => {
      calls.push({ url: '/api/me/deletion', body: await request.json() })
      return new HttpResponse(null, { status: 204 })
    }),
    http.post('/api/auth/logout', () => {
      calls.push({ url: '/api/auth/logout', body: null })
      return new HttpResponse(null, { status: 204 })
    }),
  )
  return calls
}

function notificationTexts(): string[] {
  return [...document.body.querySelectorAll('.el-notification')].map((n) => n.textContent)
}

describe('useDeleteAccount', () => {
  it('reads the second factor and the address from the session, defaulting to email', async () => {
    const guest = await withSetup(() => useDeleteAccount())
    expect(guest.result.method.value).toBe('Email')
    expect(guest.result.isAuthenticator.value).toBe(false)
    expect(guest.result.email.value).toBe('')

    const authenticator = await withSetup(() => useDeleteAccount(), {
      user: { twoFactorMethod: 'Authenticator', email: 'ada@example.test' },
    })
    expect(authenticator.result.method.value).toBe('Authenticator')
    expect(authenticator.result.isAuthenticator.value).toBe(true)
    expect(authenticator.result.email.value).toBe('ada@example.test')
  })

  it('lets members delete without asking the API whether they may', async () => {
    const { result } = await withSetup(() => useDeleteAccount(), { user: {} })

    expect(result.canDelete.value).toBe(true)
    expect(result.isLastAdmin.value).toBe(false)
  })

  it('lets an administrator delete only once the API confirms another one remains', async () => {
    server.use(http.get('/api/me/deletion', () => HttpResponse.json({ allowed: true })))
    const { result } = await withSetup(() => useDeleteAccount(), { user: { isAdmin: true } })

    await vi.waitFor(() => expect(result.canDelete.value).toBe(true))
    expect(result.isLastAdmin.value).toBe(false)
  })

  it('flags the last administrator and withholds the deletion', async () => {
    server.use(http.get('/api/me/deletion', () => HttpResponse.json({ allowed: false })))
    const { result } = await withSetup(() => useDeleteAccount(), { user: { isAdmin: true } })

    await vi.waitFor(() => expect(result.isLastAdmin.value).toBe(true))
    expect(result.canDelete.value).toBe(false)
  })

  it('asks the API for the confirmation code with the current password', async () => {
    const calls = serveDeletion()
    const { result } = await withSetup(() => useDeleteAccount(), { user: {} })

    await result.requestCode.mutateAsync('secret')

    expect(calls).toEqual([{ url: '/api/me/deletion/code', body: { currentPassword: 'secret' } }])
    expect(result.errorMessage.value).toBe('')
  })

  it('keeps the cooldown error when the code cannot be sent again', async () => {
    server.use(
      http.post('/api/me/deletion/code', () => apiError(409, 'TwoFactorResendCooldownActive')),
    )
    const { result } = await withSetup(() => useDeleteAccount(), { user: {} })

    result.requestCode.mutate('secret')

    await vi.waitFor(() =>
      expect(result.errorMessage.value).toBe(t('errors.TwoFactorResendCooldownActive')),
    )
    result.reset()
    expect(result.errorMessage.value).toBe('')
  })

  it('deletes the account, signs out, clears cached data, goes home and notifies', async () => {
    const calls = serveDeletion()
    const { result, router, queryClient } = await withSetup(() => useDeleteAccount(), {
      user: {},
      route: '/account',
    })
    queryClient.setQueryData(['cached', 'elsewhere'], 'stale')

    await result.confirm.mutateAsync({ currentPassword: 'secret', code: '123456' })

    expect(calls).toEqual([
      { url: '/api/me/deletion', body: { currentPassword: 'secret', code: '123456' } },
      { url: '/api/auth/logout', body: null },
    ])
    expect(useSession().user).toBeNull()
    expect(queryClient.getQueryData(['cached', 'elsewhere'])).toBeUndefined()
    expect(router.currentRoute.value.name).toBe('home')
    await vi.waitFor(() =>
      expect(notificationTexts().join()).toContain(
        t('features.account.deleteAccount.deletedSummary'),
      ),
    )
    expect(notificationTexts().join()).toContain(t('features.account.deleteAccount.deletedDetail'))
  })

  it('still clears the session and leaves when signing out fails afterwards', async () => {
    server.use(
      http.post('/api/me/deletion', () => new HttpResponse(null, { status: 204 })),
      http.post('/api/auth/logout', () => apiError(500)),
    )
    const { result, router } = await withSetup(() => useDeleteAccount(), {
      user: {},
      route: '/account',
    })

    await result.confirm.mutateAsync({ currentPassword: 'secret', code: '123456' })

    expect(useSession().user).toBeNull()
    expect(router.currentRoute.value.name).toBe('home')
  })

  it('keeps a rejected code visible and leaves the session untouched', async () => {
    server.use(http.post('/api/me/deletion', () => apiError(400, 'TwoFactorCodeInvalid')))
    const { result, router } = await withSetup(() => useDeleteAccount(), {
      user: {},
      route: '/account',
    })

    result.confirm.mutate({ currentPassword: 'secret', code: '000000' })

    await vi.waitFor(() => expect(result.errorMessage.value).toBe(t('errors.TwoFactorCodeInvalid')))
    expect(useSession().user).not.toBeNull()
    expect(router.currentRoute.value.name).toBe('account')
  })

  it('explains a refusal caused by content the user still authors', async () => {
    server.use(
      http.post('/api/me/deletion', () => apiError(409, 'UserDeleteAuthoredContentExists')),
    )
    const { result } = await withSetup(() => useDeleteAccount(), { user: {} })

    result.confirm.mutate({ currentPassword: 'secret', code: '123456' })

    await vi.waitFor(() =>
      expect(result.errorMessage.value).toBe(t('errors.UserDeleteAuthoredContentExists')),
    )
  })
})
