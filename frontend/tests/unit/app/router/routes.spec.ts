import { describe, expect, it } from 'vitest'
import type { RouteComponent, RouteRecordRaw } from 'vue-router'

import { routes } from '@/app/router/routes'
import { ADMIN_NAV, PRIMARY_NAV } from '@/shared/config'

import { createAppRouter } from '../../../support/render'
import { http, HttpResponse, server } from '../../../support/server'
import { buildUserResponse } from '../../../support/fixtures/user'
import { useSession } from '@/entities/session'
import { buildAuthUser } from '../../../support/fixtures/user'

function named(name: string): RouteRecordRaw {
  const route = routes.find((candidate) => candidate.name === name)
  if (!route) throw new Error(`Route ${name} not found`)
  return route
}

describe('routes', () => {
  it('lazily loads a page component for every non-redirect route', async () => {
    const loaders = routes.filter(
      (route): route is RouteRecordRaw & { component: () => Promise<RouteComponent> } =>
        typeof route.component === 'function',
    )
    expect(loaders).toHaveLength(routes.length - 1)

    const components = await Promise.all(loaders.map((route) => route.component()))

    for (const component of components) expect(component).toBeTruthy()
  }, 60_000)

  it('uses the admin layout for admin pages and the blank layout for printable pages', () => {
    const adminRoutes = routes.filter((route) => route.path.startsWith('/admin/'))

    expect(adminRoutes.length).toBeGreaterThan(0)
    for (const route of adminRoutes) {
      const expected = /\/(badges|roster)$/.test(route.path) ? 'blank' : 'admin'
      expect(route.meta?.layout).toBe(expected)
      expect(route.beforeEnter).toBeTypeOf('function')
    }
    expect(named('admin-event-roster').meta?.seo).toEqual({
      titleKey: 'seo.routes.eventRoster.title',
      noindex: true,
    })
    expect(named('event-detail').props).toBe(true)
  })

  it('references only existing route names from the navigation config', () => {
    const names = new Set(routes.map((route) => route.name))

    for (const item of [...PRIMARY_NAV, ...ADMIN_NAV]) expect(names.has(item.routeName)).toBe(true)
  })
})

describe('route guards', () => {
  it('redirects /admin to the dashboard for admins', async () => {
    server.use(
      http.get('/api/auth/me', () => HttpResponse.json(buildUserResponse({ isAdmin: true }))),
    )
    const router = createAppRouter()

    await router.push('/admin')

    expect(router.currentRoute.value.name).toBe('admin-dashboard')
  })

  it('sends guests from admin pages to login with a redirect back', async () => {
    const router = createAppRouter()

    await router.push('/admin/users')

    expect(router.currentRoute.value.name).toBe('login')
    expect(router.currentRoute.value.query).toEqual({ redirect: '/admin/users' })
  })

  it('sends guests from the account page to login with a redirect back', async () => {
    const router = createAppRouter()

    await router.push('/account')

    expect(router.currentRoute.value.name).toBe('login')
    expect(router.currentRoute.value.query).toEqual({ redirect: '/account' })
  })

  it('lets signed-in users open their account and keeps them away from guest pages', async () => {
    useSession().setUser(buildAuthUser())
    const router = createAppRouter()

    await router.push('/account')
    expect(router.currentRoute.value.name).toBe('account')

    for (const path of ['/login', '/register', '/forgot-password']) {
      await router.push('/about')
      await router.push(path)
      expect(router.currentRoute.value.name).toBe('home')
    }
  })

  it('matches unknown paths with the not-found route', async () => {
    const router = createAppRouter()

    await router.push('/does/not/exist')

    expect(router.currentRoute.value.name).toBe('not-found')
  })
})
