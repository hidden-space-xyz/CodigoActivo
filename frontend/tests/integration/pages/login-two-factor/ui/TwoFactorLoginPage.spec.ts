import { describe, expect, it, vi } from 'vitest'

import { renderApp, t } from '../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../support/server'
import { useHomeApi } from '../../../../support/api/home'
import { buildLoginChallenge, buildUserResponse } from '../../../../support/builders'

function serveChallenge(response: () => Response = () => HttpResponse.json(buildLoginChallenge())) {
  server.use(http.get('/api/auth/login/two-factor', response))
}

describe('two-factor login page', () => {
  it('explains where the code went and verifies it', async () => {
    useHomeApi()
    serveChallenge()
    const bodies: unknown[] = []
    server.use(
      http.post('/api/auth/login/two-factor', async ({ request }) => {
        bodies.push(await request.json())
        return HttpResponse.json(buildUserResponse())
      }),
    )

    const { wrapper, router } = await renderApp('/login/verify')
    await vi.waitFor(() => expect(wrapper.find('#two-factor-code').exists()).toBe(true))

    expect(wrapper.get('.two-factor-form__intro').text()).toContain('a***@example.test')
    expect(wrapper.find('.el-checkbox').exists()).toBe(false)

    await wrapper.find('#two-factor-code').setValue('123456')
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('home'), {
      timeout: 10_000,
    })
    expect(bodies).toEqual([{ code: '123456' }])
  }, 20_000)

  it('asks for the application code without a resend option for authenticator users', async () => {
    serveChallenge(() =>
      HttpResponse.json(buildLoginChallenge({ method: 'Authenticator', maskedEmail: null })),
    )

    const { wrapper } = await renderApp('/login/verify')
    await vi.waitFor(() => expect(wrapper.find('#two-factor-code').exists()).toBe(true))

    expect(wrapper.get('.two-factor-form__intro').text()).toBe(
      t('pages.loginTwoFactor.authenticatorIntro'),
    )
    expect(wrapper.find('.two-factor-resend').exists()).toBe(false)
  })

  it('counts down before offering to email the code again, then restarts the wait', async () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval', 'Date'] })
    serveChallenge()
    let resent = 0
    server.use(
      http.post('/api/auth/login/two-factor/resend', () => {
        resent += 1
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const { wrapper } = await renderApp('/login/verify')
    await vi.waitFor(() => expect(wrapper.find('.two-factor-resend__button').exists()).toBe(true))
    const resend = () => wrapper.get('.two-factor-resend__button')
    expect(resend().text()).toBe(t('pages.loginTwoFactor.resendCountdown', { s: 60 }))
    expect(resend().attributes('disabled')).toBeDefined()

    vi.advanceTimersByTime(60_000)
    await vi.waitFor(() => expect(resend().text()).toBe(t('pages.loginTwoFactor.resend')))
    expect(resend().attributes('disabled')).toBeUndefined()
    await resend().trigger('click')

    await vi.waitFor(() => expect(resent).toBe(1))
    await vi.waitFor(() =>
      expect(resend().text()).toBe(t('pages.loginTwoFactor.resendCountdown', { s: 60 })),
    )
  })

  it('shows the wrong-code error and keeps the form', async () => {
    serveChallenge()
    server.use(http.post('/api/auth/login/two-factor', () => apiError(400, 'TwoFactorCodeInvalid')))

    const { wrapper, router } = await renderApp('/login/verify')
    await vi.waitFor(() => expect(wrapper.find('#two-factor-code').exists()).toBe(true))
    await wrapper.find('#two-factor-code').setValue('000000')
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() =>
      expect(wrapper.find('[role="alert"]').text()).toBe(t('errors.TwoFactorCodeInvalid')),
    )
    expect(router.currentRoute.value.name).toBe('login-two-factor')
  })

  it('offers to log in again when there is no pending challenge', async () => {
    serveChallenge(() => apiError(401, 'TwoFactorChallengeExpired'))

    const { wrapper, router } = await renderApp('/login/verify?redirect=/events')
    await vi.waitFor(() => expect(wrapper.text()).toContain(t('pages.loginTwoFactor.expiredTitle')))

    expect(wrapper.find('#two-factor-code').exists()).toBe(false)
    expect(wrapper.get('.status-panel__actions a').attributes('href')).toBe(
      router.resolve({ name: 'login', query: { redirect: '/events' } }).href,
    )
  })

  it('marks the page as not indexable', async () => {
    serveChallenge()

    await renderApp('/login/verify')

    expect(document.title).toContain(t('seo.routes.loginTwoFactor.title'))
    expect(document.head.querySelector('meta[name="robots"]')?.getAttribute('content')).toBe(
      'noindex',
    )
  })
})
