import { describe, expect, it } from 'vitest'

import { useSession } from '@/entities/session'
import { redirectIfAuthenticated, requireAdmin, requireAuth } from '@/features/auth'

import { buildAuthUser, buildUserResponse } from '../../../../support/fixtures/user'
import { createStubRouter } from '../../../../support/render'
import { http, HttpResponse, server } from '../../../../support/server'

async function routeTo(path: string) {
  const router = createStubRouter()
  await router.push(path)
  return router.currentRoute.value
}

function countMeRequests(response: () => Response): { count: number } {
  const calls = { count: 0 }
  server.use(
    http.get('/api/auth/me', () => {
      calls.count += 1
      return response()
    }),
  )
  return calls
}

describe('redirectIfAuthenticated', () => {
  it('lets guests through', () => {
    expect(redirectIfAuthenticated()).toBe(true)
  })

  it('sends signed-in users home', () => {
    useSession().setUser(buildAuthUser())

    expect(redirectIfAuthenticated()).toEqual({ name: 'home' })
  })
})

describe('requireAuth', () => {
  it('allows a signed-in user without asking the API', async () => {
    useSession().setUser(buildAuthUser())
    const calls = countMeRequests(() => new HttpResponse(null, { status: 401 }))

    await expect(requireAuth(await routeTo('/account'))).resolves.toBe(true)
    expect(calls.count).toBe(0)
  })

  it('resolves the session from the API when the cookie is still valid', async () => {
    const calls = countMeRequests(() =>
      HttpResponse.json(buildUserResponse({ firstName: 'Grace' })),
    )

    await expect(requireAuth(await routeTo('/account'))).resolves.toBe(true)
    expect(calls.count).toBe(1)
    expect(useSession().displayName).toBe('Grace')
  })

  it('sends guests to login with the full target path as redirect', async () => {
    await expect(requireAuth(await routeTo('/account?tab=children'))).resolves.toEqual({
      name: 'login',
      query: { redirect: '/account?tab=children' },
    })
  })

  it('treats a forbidden session lookup as a guest', async () => {
    countMeRequests(() => new HttpResponse(null, { status: 403 }))

    await expect(requireAuth(await routeTo('/account'))).resolves.toEqual({
      name: 'login',
      query: { redirect: '/account' },
    })
  })
})

describe('requireAdmin', () => {
  it('sends guests to login with a redirect back to the admin page', async () => {
    await expect(requireAdmin(await routeTo('/admin/users'))).resolves.toEqual({
      name: 'login',
      query: { redirect: '/admin/users' },
    })
  })

  it('sends signed-in non-admin users home', async () => {
    useSession().setUser(buildAuthUser({ isAdmin: false }))

    await expect(requireAdmin(await routeTo('/admin/users'))).resolves.toEqual({ name: 'home' })
  })

  it('allows administrators resolved from the API', async () => {
    countMeRequests(() => HttpResponse.json(buildUserResponse({ isAdmin: true })))

    await expect(requireAdmin(await routeTo('/admin/users'))).resolves.toBe(true)
    expect(useSession().isAdmin).toBe(true)
  })
})
