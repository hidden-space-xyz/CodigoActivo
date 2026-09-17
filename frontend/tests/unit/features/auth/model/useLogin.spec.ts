import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { useSession } from '@/entities/session'
import { useLogin } from '@/features/auth'

import { mountComposable } from '../../../../support/fixtures/auth-register/composable'
import { buildLoginChallenge } from '../../../../support/fixtures/user'
import { apiError, http, HttpResponse, server } from '../../../../support/server'

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

    expect(result.form).toEqual({ identifier: '', password: '' })
    expect(result.isSubmitting.value).toBe(false)
    expect(result.isError.value).toBe(false)
  })

  it('moves to the verification page without opening a session when the password is accepted', async () => {
    const bodies = serveLogin()
    const { result, router } = await mountComposable(() => useLogin(), { route: '/login' })
    result.form.identifier = 'grace@example.test'
    result.form.password = 'secret'

    result.submit()
    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('login-two-factor'))

    expect(bodies).toEqual([{ identifier: 'grace@example.test', password: 'secret' }])
    expect(router.currentRoute.value.query).toEqual({})
    expect(useSession().isAuthenticated).toBe(false)
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

  it('flags an error and stays on the login page when the password is rejected', async () => {
    server.use(http.post('/api/auth/login', () => apiError(401, 'InvalidCredentials')))
    const { result, router } = await mountComposable(() => useLogin(), { route: '/login' })

    result.submit()
    await vi.waitFor(() => expect(result.isError.value).toBe(true))
    await flushPromises()

    expect(result.error.value).toMatchObject({ code: 'InvalidCredentials' })
    expect(useSession().isAuthenticated).toBe(false)
    expect(router.currentRoute.value.name).toBe('login')
  })
})
