import { describe, expect, it, vi } from 'vitest'

import { renderApp, t } from '../../../support/render'
import { apiError, http, server } from '../../../support/server'
import { useHomeApi } from '../../../support/api/home'
import { sessionOf } from '../../../support/session'

describe('access guard in the app', () => {
  it('sends guests from their account to login, remembering where they were going', async () => {
    const { wrapper, router } = await renderApp('/account?tab=children')

    expect(router.currentRoute.value.name).toBe('login')
    expect(router.currentRoute.value.query).toEqual({ redirect: '/account?tab=children' })
    expect(wrapper.text()).toContain(t('pages.login.title'))
  })

  it('sends guests from the admin area to login, coming back to it', async () => {
    const { router } = await renderApp('/admin')

    expect(router.currentRoute.value.name).toBe('login')
    expect(router.currentRoute.value.query).toEqual({ redirect: '/admin' })
  })

  it('sends signed-in users without the needed cargo away from admin pages', async () => {
    useHomeApi()

    const { router } = await renderApp('/admin/users', { user: { staffRole: 'Editor' } })

    expect(router.currentRoute.value.name).toBe('home')
  })

  it('stays anonymous when the session lookup fails', async () => {
    useHomeApi()
    server.use(http.get('/api/auth/me', () => apiError(403)))

    const { wrapper, queryClient } = await renderApp('/')

    await vi.waitFor(() => expect(wrapper.find('.header__login').exists()).toBe(true))
    expect(sessionOf(queryClient).isAuthenticated).toBe(false)
  })
})
