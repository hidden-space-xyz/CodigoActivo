import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { useSession } from '@/entities/session'
import { useLogin } from '@/features/auth'

import { mountComposable } from '../../../../support/fixtures/auth-register/composable'
import { buildUserResponse } from '../../../../support/fixtures/user'
import { apiError, http, HttpResponse, server } from '../../../../support/server'

function serveLogin() {
  const bodies: unknown[] = []
  server.use(
    http.post('/api/auth/login', async ({ request }) => {
      bodies.push(await request.json())
      return HttpResponse.json(buildUserResponse({ firstName: 'Grace', isAdmin: true }))
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

  it('stores the user and navigates home when there is no redirect', async () => {
    const bodies = serveLogin()
    const { result, router } = await mountComposable(() => useLogin(), { route: '/login' })
    result.form.identifier = 'grace@example.test'
    result.form.password = 'secret'

    result.submit()
    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('home'))

    expect(bodies).toEqual([{ identifier: 'grace@example.test', password: 'secret' }])
    expect(useSession().isAdmin).toBe(true)
    expect(useSession().displayName).toBe('Grace')
  })

  it('ignores a redirect given more than once and navigates home', async () => {
    serveLogin()
    const { result, router } = await mountComposable(() => useLogin(), {
      route: '/login?redirect=/about&redirect=/events',
    })

    result.submit()
    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('home'))
  })

  it('navigates to the redirect path after signing in', async () => {
    serveLogin()
    const { result, router } = await mountComposable(() => useLogin(), {
      route: '/login?redirect=/events',
    })

    result.submit()
    await vi.waitFor(() => expect(router.currentRoute.value.fullPath).toBe('/events'))
  })

  it('flags an error and keeps the session empty when login fails', async () => {
    server.use(http.post('/api/auth/login', () => apiError(401, 'InvalidCredentials')))
    const { result, router } = await mountComposable(() => useLogin(), { route: '/login' })

    result.submit()
    await vi.waitFor(() => expect(result.isError.value).toBe(true))
    await flushPromises()

    expect(useSession().isAuthenticated).toBe(false)
    expect(router.currentRoute.value.name).toBe('login')
  })
})
