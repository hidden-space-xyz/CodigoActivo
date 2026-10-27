import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { useLogin } from '@/pages/login/model/use-login'

import { apiError, http, HttpResponse, server } from '../../../../support/server'
import { buildLoginChallenge } from '../../../../support/builders'
import { mountComposable, t } from '../../../../support/render'
import { sessionOf } from '../../../../support/session'

function serveLogin() {
  const bodies: unknown[] = []
  server.use(
    http.post('/api/auth/login', async ({ request }) => {
      bodies.push(await request.json())
      return HttpResponse.json(buildLoginChallenge())
    }),
  )
  return bodies
}

describe('useLogin', () => {
  it('starts with empty credentials', async () => {
    const { result } = await mountComposable(() => useLogin(), { route: '/login' })

    expect(result.form).toEqual({ identifier: '', password: '', keepSignedIn: false })
    expect(result.isSubmitting.value).toBe(false)
    expect(result.errorMessage.value).toBeNull()
  })

  it('moves to the verification page without opening a session when the password is accepted', async () => {
    const bodies = serveLogin()
    const { result, router, queryClient } = await mountComposable(() => useLogin(), {
      route: '/login',
    })
    result.form.identifier = 'grace@example.test'
    result.form.password = 'secret'

    result.submit()
    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('login-two-factor'))

    expect(bodies).toEqual([
      { identifier: 'grace@example.test', password: 'secret', keepSignedIn: false },
    ])
    expect(router.currentRoute.value.query).toEqual({})
    expect(sessionOf(queryClient).isAuthenticated).toBe(false)
  })

  it('asks for a persistent session only when the user opts in', async () => {
    const bodies = serveLogin()
    const { result, router } = await mountComposable(() => useLogin(), { route: '/login' })
    result.form.identifier = 'grace@example.test'
    result.form.password = 'secret'
    result.form.keepSignedIn = true

    result.submit()
    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('login-two-factor'))

    expect(bodies).toEqual([
      { identifier: 'grace@example.test', password: 'secret', keepSignedIn: true },
    ])
  })

  it('carries the redirect target over to the verification page', async () => {
    serveLogin()
    const { result, router } = await mountComposable(() => useLogin(), {
      route: '/login?redirect=/events',
    })

    result.submit()
    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('login-two-factor'))

    expect(router.currentRoute.value.query).toEqual({ redirect: '/events' })
  })

  it('ignores a redirect given more than once', async () => {
    serveLogin()
    const { result, router } = await mountComposable(() => useLogin(), {
      route: '/login?redirect=/about&redirect=/events',
    })

    result.submit()
    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('login-two-factor'))

    expect(router.currentRoute.value.query).toEqual({})
  })

  it.each(['//evil.test', 'https://evil.test', '/\\evil.test'])(
    'ignores the hostile redirect %s',
    async (redirect) => {
      serveLogin()
      const { result, router } = await mountComposable(() => useLogin(), {
        route: `/login?redirect=${encodeURIComponent(redirect)}`,
      })

      result.submit()
      await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('login-two-factor'))

      expect(router.currentRoute.value.query).toEqual({})
    },
  )

  it('honours a redirect to /account', async () => {
    serveLogin()
    const { result, router } = await mountComposable(() => useLogin(), {
      route: '/login?redirect=/account',
    })

    result.submit()
    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('login-two-factor'))

    expect(router.currentRoute.value.query).toEqual({ redirect: '/account' })
  })

  it('shows the generic message and stays on the login page when the password is rejected', async () => {
    server.use(http.post('/api/auth/login', () => apiError(401, 'InvalidCredentials')))
    const { result, router, queryClient } = await mountComposable(() => useLogin(), {
      route: '/login',
    })

    result.submit()
    await vi.waitFor(() => expect(result.errorMessage.value).toBe(t('pages.login.error')))
    await flushPromises()

    expect(sessionOf(queryClient).isAuthenticated).toBe(false)
    expect(router.currentRoute.value.name).toBe('login')
  })

  it('keeps the generic message for a failure without a known code', async () => {
    server.use(http.post('/api/auth/login', () => apiError(500)))
    const { result } = await mountComposable(() => useLogin(), { route: '/login' })

    result.submit()

    await vi.waitFor(() => expect(result.errorMessage.value).toBe(t('pages.login.error')))
  })

  it.each([
    [403, 'UserAccountPendingVerification'],
    [403, 'UserAccountBlocked'],
    [403, 'TwoFactorLocked'],
    [429, 'TooManyRequests'],
  ] as const)(
    'explains the %i %s refusal instead of blaming the credentials',
    async (status, code) => {
      server.use(http.post('/api/auth/login', () => apiError(status, code)))
      const { result, router } = await mountComposable(() => useLogin(), { route: '/login' })

      result.submit()

      await vi.waitFor(() => expect(result.errorMessage.value).toBe(t(`errors.${code}`)))
      expect(router.currentRoute.value.name).toBe('login')
    },
  )
})
