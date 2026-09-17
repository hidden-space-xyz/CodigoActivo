import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { useSession } from '@/entities/session'

import { useHomeApi } from '../../support/fixtures/auth-register/home'
import { buildUserResponse } from '../../support/fixtures/user'
import { renderApp, t } from '../../support/render'
import { apiError, http, HttpResponse, server, TEST_CSRF_TOKEN } from '../../support/server'

describe('login page', () => {
  it('signs in and navigates to the redirect target', async () => {
    let received: { body: unknown; csrf: string | null } | undefined
    server.use(
      http.post('/api/auth/login', async ({ request }) => {
        received = { body: await request.json(), csrf: request.headers.get('X-CSRF-TOKEN') }
        return HttpResponse.json(buildUserResponse({ firstName: 'Grace' }))
      }),
    )

    const { wrapper, router } = await renderApp('/login?redirect=/about')
    expect(wrapper.text()).toContain(t('pages.login.title'))

    await wrapper.find('#login-identifier').setValue('grace@example.test')
    await wrapper.find('#login-password').setValue('secret')
    await wrapper.find('form').trigger('submit')
    await flushPromises()
    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('about'))

    expect(received).toEqual({
      body: { identifier: 'grace@example.test', password: 'secret' },
      csrf: TEST_CSRF_TOKEN,
    })
    expect(useSession().displayName).toBe('Grace')
  })

  it('shows an error when the credentials are rejected', async () => {
    server.use(http.post('/api/auth/login', () => apiError(401)))

    const { wrapper, router } = await renderApp('/login')
    await wrapper.find('#login-identifier').setValue('nobody')
    await wrapper.find('#login-password').setValue('wrong')
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() => expect(wrapper.find('[role="alert"]').exists()).toBe(true))
    expect(wrapper.find('[role="alert"]').text()).toBe(t('pages.login.error'))
    expect(router.currentRoute.value.name).toBe('login')
  })

  it('goes home after signing in without a redirect and greets the user in the header', async () => {
    useHomeApi()
    server.use(
      http.post('/api/auth/login', () =>
        HttpResponse.json(buildUserResponse({ firstName: 'Grace' })),
      ),
    )

    const { wrapper, router } = await renderApp('/login')
    await wrapper.find('#login-identifier').setValue('grace@example.test')
    await wrapper.find('#login-password').setValue('secret')
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('home'))
    await vi.waitFor(() =>
      expect(wrapper.text()).toContain(t('common.greeting', { name: 'Grace' })),
    )
  })

  it('links to password recovery and registration', async () => {
    const { wrapper, router } = await renderApp('/login')
    const main = wrapper.get('main')

    expect(main.get('.login-forgot').attributes('href')).toBe(
      router.resolve({ name: 'forgot-password' }).href,
    )
    expect(main.get('.login-alt__link').attributes('href')).toBe(
      router.resolve({ name: 'register' }).href,
    )
  })

  it('sends signed-in users home', async () => {
    useHomeApi()

    const { router } = await renderApp('/login', { user: {} })

    expect(router.currentRoute.value.name).toBe('home')
  })

  it('marks the page as not indexable', async () => {
    await renderApp('/login')

    expect(document.title).toContain(t('seo.routes.login.title'))
    expect(document.head.querySelector('meta[name="robots"]')?.getAttribute('content')).toBe(
      'noindex',
    )
  })
})
