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
  it('lazily loads a page component for every route but the /admin entry', async () => {
    const loaders = routes.filter(
      (route): route is RouteRecordRaw & { component: () => Promise<RouteComponent> } =>
        typeof route.component === 'function',
    )
    expect(loaders).toHaveLength(routes.length - 1)

    const components = await Promise.all(loaders.map((route) => route.component()))

    for (const component of components) expect(component).toBeTruthy()
  }, 60_000)

  it('keeps admin pages for their staff, in the admin layout or blank when printable', () => {
    const adminRoutes = routes.filter((route) => route.path.startsWith('/admin/'))
    const otherAccess: Record<string, string> = {
      'admin-events': 'editor',
      'admin-event-detail': 'editor',
      'admin-news': 'editor',
      'admin-partners': 'editor',
      'admin-resources': 'editor',
      'admin-catalogs': 'editor',
      'admin-audit': 'audit',
    }

    expect(adminRoutes.length).toBeGreaterThan(0)
    for (const route of adminRoutes) {
      const expected = /\/(badges|roster)$/.test(route.path) ? 'blank' : 'admin'
      expect(route.meta?.layout).toBe(expected)
      const name = String(route.name)
      expect(route.meta?.access, name).toBe(otherAccess[name] ?? 'admin')
    }
    expect(named('admin').meta?.access).toBe('user')
    expect(named('admin-event-roster').meta?.seo).toEqual({
      titleKey: 'seo.routes.eventRoster.title',
      noindex: true,
    })
    expect(named('event-detail').props).toBe(true)
    expect(named('admin-diagnostics').path).toBe('/admin/diagnostics')
    expect(named('admin-audit').path).toBe('/admin/audit')
    expect(named('admin-blocked-data').path).toBe('/admin/blocked-data')
  })

  it('keeps guest pages for guests and the account for signed-in users', () => {
    for (const name of ['login', 'login-two-factor', 'register', 'forgot-password']) {
      expect(named(name).meta?.access).toBe('guest')
    }
    expect(named('account').meta?.access).toBe('user')
    expect(named('reset-password').meta?.access).toBeUndefined()
    expect(named('verify-account').meta?.access).toBeUndefined()
  })

  it('files each detail page under the header section of its list', () => {
    expect(named('events').meta?.section).toBe('events')
    expect(named('event-detail').meta?.section).toBe('events')
    expect(named('news').meta?.section).toBe('news')
    expect(named('news-detail').meta?.section).toBe('news')
    expect(named('resources').meta?.section).toBe('resources')
    expect(named('resource-detail').meta?.section).toBe('resources')
  })

  it('references only existing route names from the navigation config', () => {
    const names = new Set(routes.map((route) => route.name))

    for (const item of [...PRIMARY_NAV, ...ADMIN_NAV]) expect(names.has(item.routeName)).toBe(true)
  })

  it('shows each admin sidebar entry to the access its route needs', () => {
    for (const item of ADMIN_NAV) expect(named(item.routeName).meta?.access).toBe(item.access)
  })
})

describe('application router', () => {
  it('leads /admin to the dashboard for admins and to the events for editors', async () => {
    const landing = async (staffRole: 'Administrator' | 'Editor') => {
      const queryClient = createTestQueryClient()
      startSession(queryClient, buildAuthUser({ staffRole }))
      const router = createAppRouter(queryClient)
      await router.push('/admin')
      return router.currentRoute.value.name
    }

    expect(await landing('Administrator')).toBe('admin-dashboard')
    expect(await landing('Editor')).toBe('admin-events')
  })

  it('matches unknown paths with the not-found route', async () => {
    const router = createAppRouter(createTestQueryClient())

    await router.push('/does/not/exist')

    expect(router.currentRoute.value.name).toBe('not-found')
  })

  it('opens detail pages only for GUID ids', async () => {
    const router = createAppRouter(createTestQueryClient())

    await router.push('/events/3F2504E0-4F89-41D3-9A0C-0305E82C3301')
    expect(router.currentRoute.value.name).toBe('event-detail')

    for (const path of ['/events/abc%2Ffeature', '/news/abc%3Fx%3D1', '/resources/abc%23']) {
      await router.push(path)
      expect(router.currentRoute.value.name).toBe('not-found')
    }
  })
})
