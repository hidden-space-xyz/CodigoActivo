import { describe, expect, it, vi } from 'vitest'

import { useHomeApi } from '../../support/fixtures/auth-register/home'
import { renderApp, t } from '../../support/render'
import { apiError, http, HttpResponse, server, TEST_CSRF_TOKEN } from '../../support/server'

describe('forgot password page', () => {
  it('requests a reset link for the trimmed email and confirms it was sent', async () => {
    const received: { body: unknown; csrf: string | null }[] = []
    server.use(
      http.post('/api/auth/forgot-password', async ({ request }) => {
        received.push({ body: await request.json(), csrf: request.headers.get('X-CSRF-TOKEN') })
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { wrapper } = await renderApp('/forgot-password')
    expect(wrapper.text()).toContain(t('pages.forgotPassword.title'))

    await wrapper.find('#forgot-email').setValue('  ada@example.test ')
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() => expect(wrapper.text()).toContain(t('pages.forgotPassword.sentTitle')))
    expect(received).toEqual([{ body: { email: 'ada@example.test' }, csrf: TEST_CSRF_TOKEN }])
    expect(wrapper.find('form').exists()).toBe(false)
    expect(wrapper.get('.forgot-sent').text()).toContain(
      t('pages.forgotPassword.sentExpiry', { minutes: 15 }),
    )
  })

  it('shows an error and keeps the form when the request fails', async () => {
    server.use(http.post('/api/auth/forgot-password', () => apiError(500)))
    const { wrapper } = await renderApp('/forgot-password')

    await wrapper.find('#forgot-email').setValue('ada@example.test')
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() =>
      expect(wrapper.find('[role="alert"]').text()).toBe(t('pages.forgotPassword.error')),
    )
    expect(wrapper.find('form').exists()).toBe(true)
  })

  it('links back to login', async () => {
    const { wrapper, router } = await renderApp('/forgot-password')

    expect(wrapper.get('.forgot-alt__link').attributes('href')).toBe(
      router.resolve({ name: 'login' }).href,
    )
  })

  it('sends signed-in users home', async () => {
    useHomeApi()

    const { router } = await renderApp('/forgot-password', { user: {} })

    expect(router.currentRoute.value.name).toBe('home')
  })
})
