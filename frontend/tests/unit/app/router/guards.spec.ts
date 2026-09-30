import { defineComponent, h } from 'vue'
import { describe, expect, it } from 'vitest'
import { createMemoryHistory, createRouter, type RouteMeta } from 'vue-router'
import type { QueryClient } from '@tanstack/vue-query'

import { installAccessGuard } from '@/app/router/guards'
import { startSession } from '@/entities/session'

import { createTestQueryClient } from '../../../support/render'
import { http, HttpResponse, server } from '../../../support/server'
import { buildUserResponse } from '../../../support/builders'
import { buildAuthUser } from '../../../support/models'
import { sessionOf } from '../../../support/session'

const Page = defineComponent({ render: () => h('div') })

function routerFor(queryClient: QueryClient) {
  const route = (path: string, name: string, meta: RouteMeta = {}) => ({
    path,
    name,
    meta,
    component: Page,
  })
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      route('/', 'home'),
      route('/login', 'login', { access: 'guest' }),
      route('/account', 'account', { access: 'user' }),
      route('/admin/users', 'admin-users', { access: 'admin' }),
    ],
  })
  installAccessGuard(router, queryClient)
  return router
}

async function navigate(path: string, queryClient = createTestQueryClient()) {
  const router = routerFor(queryClient)
  await router.push(path)
  return { route: router.currentRoute.value, queryClient }
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

describe('installAccessGuard', () => {
  it('lets anyone open routes without an access rule', async () => {
    const calls = countMeRequests(() => new HttpResponse(null, { status: 401 }))

    const { route } = await navigate('/')

    expect(route.name).toBe('home')
    expect(calls.count).toBe(0)
  })

  it('lets guests open guest pages and sends known users home without asking the API', async () => {
    const calls = countMeRequests(() => new HttpResponse(null, { status: 401 }))
    expect((await navigate('/login')).route.name).toBe('login')

    const queryClient = createTestQueryClient()
    startSession(queryClient, buildAuthUser())

    expect((await navigate('/login', queryClient)).route.name).toBe('home')
    expect(calls.count).toBe(0)
  })

  it('lets a known user open protected pages without asking the API', async () => {
    const calls = countMeRequests(() => new HttpResponse(null, { status: 401 }))
    const queryClient = createTestQueryClient()
    startSession(queryClient, buildAuthUser())

    expect((await navigate('/account', queryClient)).route.name).toBe('account')
    expect(calls.count).toBe(0)
  })

  it('resolves the session from the API when the cookie is still valid', async () => {
    const calls = countMeRequests(() =>
      HttpResponse.json(buildUserResponse({ firstName: 'Grace' })),
    )

    const { route, queryClient } = await navigate('/account')

    expect(route.name).toBe('account')
    expect(calls.count).toBe(1)
    expect(sessionOf(queryClient).displayName).toBe('Grace')
  })

  it('sends guests to login with the full target path as redirect', async () => {
    const { route } = await navigate('/account?tab=children')

    expect(route.name).toBe('login')
    expect(route.query).toEqual({ redirect: '/account?tab=children' })
  })

  it('treats a forbidden session lookup as a guest', async () => {
    countMeRequests(() => new HttpResponse(null, { status: 403 }))

    const { route } = await navigate('/account')

    expect(route.name).toBe('login')
    expect(route.query).toEqual({ redirect: '/account' })
  })

  it('asks the API again for a guest on every protected navigation', async () => {
    const calls = countMeRequests(() => new HttpResponse(null, { status: 401 }))
    const queryClient = createTestQueryClient()

    await navigate('/account', queryClient)
    await navigate('/account', queryClient)

    expect(calls.count).toBe(2)
  })

  it('sends signed-in non-admin users home from admin pages', async () => {
    const queryClient = createTestQueryClient()
    startSession(queryClient, buildAuthUser({ isAdmin: false }))

    expect((await navigate('/admin/users', queryClient)).route.name).toBe('home')
  })

  it('lets administrators resolved from the API open admin pages', async () => {
    countMeRequests(() => HttpResponse.json(buildUserResponse({ isAdmin: true })))

    const { route, queryClient } = await navigate('/admin/users')

    expect(route.name).toBe('admin-users')
    expect(sessionOf(queryClient).isAdmin).toBe(true)
  })
})
