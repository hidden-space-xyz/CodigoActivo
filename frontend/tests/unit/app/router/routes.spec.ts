import { describe, expect, it } from 'vitest'
import type { RouteComponent, RouteRecordRaw } from 'vue-router'

import { routes } from '@/app/router/routes'
import { startSession } from '@/entities/session'
import { ADMIN_NAV } from '@/widgets/admin-shell/config/navigation'
import { PRIMARY_NAV } from '@/widgets/site-header/config/navigation'

import { createAppRouter, createTestQueryClient } from '../../../support/render'
import { buildAuthUser } from '../../../support/models'

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

  it('keeps admin pages for administrators, in the admin layout or blank when printable', () => {
    const adminRoutes = routes.filter((route) => route.path.startsWith('/admin/'))

    expect(adminRoutes.length).toBeGreaterThan(0)
    for (const route of adminRoutes) {
      const expected = /\/(badges|roster)$/.test(route.path) ? 'blank' : 'admin'
      expect(route.meta?.layout).toBe(expected)
      expect(route.meta?.access).toBe('admin')
    }
    expect(named('admin-event-roster').meta?.seo).toEqual({
      titleKey: 'seo.routes.eventRoster.title',
      noindex: true,
    })
    expect(named('event-detail').props).toBe(true)
  })

  it('keeps guest pages for guests and the account for signed-in users', () => {
    for (const name of ['login', 'login-two-factor', 'register', 'forgot-password']) {
      expect(named(name).meta?.access).toBe('guest')
    }
    expect(named('account').meta?.access).toBe('user')
    expect(named('reset-password').meta?.access).toBeUndefined()
    expect(named('verify-account').meta?.access).toBeUndefined()
  })

  it('files event details under the events section of the header', () => {
    expect(named('events').meta?.section).toBe('events')
    expect(named('event-detail').meta?.section).toBe('events')
  })

  it('references only existing route names from the navigation config', () => {
    const names = new Set(routes.map((route) => route.name))

    for (const item of [...PRIMARY_NAV, ...ADMIN_NAV]) expect(names.has(item.routeName)).toBe(true)
  })
})

describe('application router', () => {
  it('redirects /admin to the dashboard for admins', async () => {
    const queryClient = createTestQueryClient()
    startSession(queryClient, buildAuthUser({ isAdmin: true }))
    const router = createAppRouter(queryClient)

    await router.push('/admin')

    expect(router.currentRoute.value.name).toBe('admin-dashboard')
  })

  it('matches unknown paths with the not-found route', async () => {
    const router = createAppRouter(createTestQueryClient())

    await router.push('/does/not/exist')

    expect(router.currentRoute.value.name).toBe('not-found')
  })
})
