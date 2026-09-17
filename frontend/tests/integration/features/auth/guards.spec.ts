import { describe, expect, it, vi } from 'vitest'

import { useSession } from '@/entities/session'

import { useHomeApi } from '../../../support/fixtures/auth-register/home'
import { renderApp, t } from '../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../support/server'

describe('route guards in the app', () => {
  it('sends guests from their account to login and back after signing in', async () => {
    const { wrapper, router } = await renderApp('/account?tab=children')

    expect(router.currentRoute.value.name).toBe('login')
    expect(router.currentRoute.value.query).toEqual({ redirect: '/account?tab=children' })
    expect(wrapper.text()).toContain(t('pages.login.title'))
  })

  it('sends guests from admin pages to login, following the admin redirect', async () => {
    const { router } = await renderApp('/admin')

    expect(router.currentRoute.value.name).toBe('login')
    expect(router.currentRoute.value.query).toEqual({ redirect: '/admin/dashboard' })
  })

  it('sends signed-in non-admin users away from admin pages', async () => {
    useHomeApi()

    const { router } = await renderApp('/admin/users', { user: { isAdmin: false } })

    expect(router.currentRoute.value.name).toBe('home')
  })

  it('logs out from the header and lands on login', async () => {
    useHomeApi()
    let logoutCalls = 0
    server.use(
      http.post('/api/auth/logout', () => {
        logoutCalls += 1
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { wrapper, router } = await renderApp('/', { user: { firstName: 'Grace' } })
    expect(wrapper.text()).toContain(t('common.greeting', { name: 'Grace' }))

    await wrapper.get('.header__cta').trigger('click')

    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('login'))
    expect(logoutCalls).toBe(1)
    expect(useSession().isAuthenticated).toBe(false)
    await vi.waitFor(() =>
      expect(wrapper.text()).not.toContain(t('common.greeting', { name: 'Grace' })),
    )
  })

  it('bootstraps the session on startup from the auth cookie', async () => {
    useHomeApi()
    server.use(
      http.get('/api/auth/me', () =>
        HttpResponse.json({ id: 'u1', firstName: 'Linus', isAdmin: false }),
      ),
    )

    const { wrapper } = await renderApp('/')

    await vi.waitFor(() =>
      expect(wrapper.text()).toContain(t('common.greeting', { name: 'Linus' })),
    )
  })

  it('stays anonymous when the session lookup fails', async () => {
    useHomeApi()
    server.use(http.get('/api/auth/me', () => apiError(403)))

    const { wrapper } = await renderApp('/')

    await vi.waitFor(() => expect(wrapper.find('.header__login').exists()).toBe(true))
    expect(useSession().isAuthenticated).toBe(false)
  })
})
