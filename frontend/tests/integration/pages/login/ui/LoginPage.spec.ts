import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { renderApp, t } from '../../../../support/render'
import { apiError, http, HttpResponse, server, TEST_CSRF_TOKEN } from '../../../../support/server'
import { buildLoginChallenge } from '../../../../support/builders'
import { sessionOf } from '../../../../support/session'

function serveChallenge() {
  server.use(http.get('/api/auth/login/two-factor', () => HttpResponse.json(buildLoginChallenge())))
}

describe('login page', () => {
  it('accepts the password and continues to the verification page keeping the redirect', async () => {
    let received: { body: unknown; csrf: string | null } | undefined
    server.use(
      http.post('/api/auth/login', async ({ request }) => {
        received = { body: await request.json(), csrf: request.headers.get('X-CSRF-TOKEN') }
        return HttpResponse.json(buildLoginChallenge())
      }),
    )
    serveChallenge()

    const { wrapper, router, queryClient } = await renderApp('/login?redirect=/about')
    await wrapper.find('#login-identifier').setValue('grace@example.test')
    await wrapper.find('#login-password').setValue('secret')
    await wrapper.find('form').trigger('submit')
    await flushPromises()
    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('login-two-factor'))

    expect(received).toEqual({
      body: { identifier: 'grace@example.test', password: 'secret', keepSignedIn: false },
      csrf: TEST_CSRF_TOKEN,
    })
    expect(router.currentRoute.value.query).toEqual({ redirect: '/about' })
    expect(sessionOf(queryClient).isAuthenticated).toBe(false)
    await vi.waitFor(() => expect(wrapper.text()).toContain(t('pages.loginTwoFactor.title')))
  })

  it('keeps the session after closing the browser only when the user ticks the box', async () => {
    const bodies: unknown[] = []
    server.use(
      http.post('/api/auth/login', async ({ request }) => {
        bodies.push(await request.json())
        return HttpResponse.json(buildLoginChallenge())
      }),
    )
    serveChallenge()

    const { wrapper, router } = await renderApp('/login')

    const keep = wrapper.get('#login-keep-signed-in')
    expect((keep.element as HTMLInputElement).checked).toBe(false)
    expect(wrapper.get('.login-keep__label').attributes('for')).toBe('login-keep-signed-in')
    expect(wrapper.find('.login-keep__hint').exists()).toBe(false)

    await wrapper.find('#login-identifier').setValue('grace@example.test')
    await wrapper.find('#login-password').setValue('secret')
    await keep.setValue(true)
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() =>
      expect(bodies).toEqual([
        { identifier: 'grace@example.test', password: 'secret', keepSignedIn: true },
      ]),
    )
    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('login-two-factor'))
  })

  it('shows an error when the credentials are rejected', async () => {
    server.use(http.post('/api/auth/login', () => apiError(401, 'InvalidCredentials')))

    const { wrapper, router } = await renderApp('/login')
    await wrapper.find('#login-identifier').setValue('nobody')
    await wrapper.find('#login-password').setValue('wrong')
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() => expect(wrapper.find('[role="alert"]').exists()).toBe(true))
    expect(wrapper.find('[role="alert"]').text()).toBe(t('pages.login.error'))
    expect(router.currentRoute.value.name).toBe('login')
  })

  it('marks the page as not indexable', async () => {
    await renderApp('/login')

    expect(document.title).toContain(t('seo.routes.login.title'))
    expect(document.head.querySelector('meta[name="robots"]')?.getAttribute('content')).toBe(
      'noindex',
    )
  })
})
