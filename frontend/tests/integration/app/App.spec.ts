import { describe, expect, it, vi } from 'vitest'

import App from '@/app/App.vue'
import { useSession } from '@/entities/session'

import { buildUserResponse } from '../../support/fixtures/user'
import { renderApp, renderWithProviders, t } from '../../support/render'
import { http, HttpResponse, server } from '../../support/server'

describe('App', () => {
  it('renders public pages inside the default layout and resolves the session on mount', async () => {
    let meCalls = 0
    server.use(
      http.get('/api/auth/me', () => {
        meCalls += 1
        return HttpResponse.json(buildUserResponse({ firstName: 'Grace' }))
      }),
    )

    const { wrapper } = await renderWithProviders(App, { route: '/about', attach: true })

    expect(wrapper.find('.layout').exists()).toBe(true)
    expect(wrapper.find('header.header').exists()).toBe(true)
    expect(wrapper.find('main.layout__main [data-testid="route-stub"]').exists()).toBe(true)
    expect(wrapper.find('.admin').exists()).toBe(false)
    await vi.waitFor(() => expect(useSession().displayName).toBe('Grace'))
    expect(meCalls).toBe(1)
    await vi.waitFor(() =>
      expect(wrapper.find('.header__greeting').text()).toBe(
        t('common.greeting', { name: 'Grace' }),
      ),
    )
  })

  it('renders admin pages inside the admin layout', async () => {
    const { wrapper } = await renderWithProviders(App, {
      route: '/admin/users',
      user: { isAdmin: true },
      attach: true,
    })

    expect(wrapper.find('.admin').exists()).toBe(true)
    expect(wrapper.find('.admin__main [data-testid="route-stub"]').exists()).toBe(true)
    expect(wrapper.find('.layout').exists()).toBe(false)
  })

  it('renders printable pages without layout chrome', async () => {
    const { wrapper } = await renderWithProviders(App, {
      route: '/admin/events/event-1/roster',
      user: { isAdmin: true },
      attach: true,
    })

    expect(wrapper.find('[data-testid="route-stub"]').exists()).toBe(true)
    expect(wrapper.find('.admin').exists()).toBe(false)
    expect(wrapper.find('.layout').exists()).toBe(false)
  })

  it('switches layouts when navigating between public and admin areas', async () => {
    const { wrapper, router } = await renderWithProviders(App, {
      route: '/events',
      user: { isAdmin: true },
      attach: true,
    })
    expect(wrapper.find('.layout').exists()).toBe(true)

    await router.push('/admin/dashboard')

    expect(wrapper.find('.admin').exists()).toBe(true)
    expect(wrapper.find('.layout').exists()).toBe(false)
  })

  it('shows the real not-found page for unknown paths', async () => {
    const { wrapper, router } = await renderApp('/missing/page')

    expect(router.currentRoute.value.name).toBe('not-found')
    expect(wrapper.find('main.layout__main').text()).not.toBe('')
    expect(document.title).toContain(t('seo.routes.notFound.title'))
  })
})
