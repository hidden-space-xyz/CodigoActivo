import { flushPromises } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import { useSession } from '@/entities/session'
import { useAuth } from '@/features/auth'

import { mountComposable } from '../../../../support/fixtures/auth-register/composable'
import { buildUserResponse } from '../../../../support/fixtures/user'
import { apiError, http, HttpResponse, server } from '../../../../support/server'

describe('useAuth', () => {
  it('exposes the session flags of the signed-in user', async () => {
    const { result } = await mountComposable(() => useAuth(), {
      user: { firstName: 'Grace', isAdmin: true },
    })

    expect(result.isAuthenticated.value).toBe(true)
    expect(result.isAdmin.value).toBe(true)
    expect(result.displayName.value).toBe('Grace')
  })

  it('bootstraps the session from the current user endpoint', async () => {
    server.use(http.get('/api/auth/me', () => HttpResponse.json(buildUserResponse())))
    const { result } = await mountComposable(() => useAuth())
    expect(result.isAuthenticated.value).toBe(false)

    await result.bootstrap()

    expect(result.isAuthenticated.value).toBe(true)
    expect(result.displayName.value).toBe('Ada')
  })

  it('logs out, clears the session and cached queries, and navigates to login', async () => {
    let logoutCalls = 0
    server.use(
      http.post('/api/auth/logout', () => {
        logoutCalls += 1
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { result, router, queryClient } = await mountComposable(() => useAuth(), {
      user: {},
      route: '/account',
    })
    queryClient.setQueryData(['private'], { secret: true })

    await result.logout()

    expect(logoutCalls).toBe(1)
    expect(useSession().isAuthenticated).toBe(false)
    expect(queryClient.getQueryData(['private'])).toBeUndefined()
    expect(router.currentRoute.value.name).toBe('login')
  })

  it('still clears the session and navigates to login when the logout request fails', async () => {
    server.use(http.post('/api/auth/logout', () => apiError(500)))
    const { result, router, queryClient } = await mountComposable(() => useAuth(), {
      user: {},
      route: '/account',
    })
    queryClient.setQueryData(['private'], { secret: true })

    await expect(result.logout()).rejects.toThrow()
    await flushPromises()

    expect(useSession().isAuthenticated).toBe(false)
    expect(queryClient.getQueryData(['private'])).toBeUndefined()
    expect(router.currentRoute.value.name).toBe('login')
  })
})
