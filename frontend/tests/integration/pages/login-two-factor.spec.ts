import { describe, expect, it, vi } from 'vitest'

import { useHomeApi } from '../../support/fixtures/auth-register/home'
import { buildLoginChallenge, buildUserResponse } from '../../support/fixtures/user'
import { renderApp, t } from '../../support/render'
import { apiError, http, HttpResponse, server } from '../../support/server'

function serveChallenge(response: () => Response = () => HttpResponse.json(buildLoginChallenge())) {
  server.use(http.get('/api/auth/login/two-factor', response))
}

describe('two-factor login page', () => {
  it('explains where the code went and verifies it', async () => {
    useHomeApi()
    serveChallenge()
    server.use(
      http.post('/api/auth/login/two-factor', () => HttpResponse.json(buildUserResponse())),
    )

    const { wrapper, router } = await renderApp('/login/verify')
    await vi.waitFor(() => expect(wrapper.find('#two-factor-code').exists()).toBe(true))

    const intro = wrapper.get('.two-factor-form__intro').text()
    expect(intro).toContain('a***@example.test')
    expect(intro).toContain(t('pages.loginTwoFactor.emailIntroBefore').trim())
    expect(wrapper.text()).toContain(t('pages.loginTwoFactor.resendPrompt'))

    await wrapper.find('#two-factor-code').setValue('123456')
    await wrapper.find('form').trigger('submit')

    // The home page chunk loads lazily, which can take a while on slow file systems.
    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('home'), {
      timeout: 10_000,
    })
  }, 20_000)

  it('keeps the session after closing the browser only when the user ticks the box', async () => {
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

    const keep = wrapper.get('#two-factor-keep-signed-in')
    expect((keep.element as HTMLInputElement).checked).toBe(false)
    const label = wrapper.get('.two-factor-keep__label')
    expect(label.attributes('for')).toBe('two-factor-keep-signed-in')
    expect(label.text()).toBe(t('pages.loginTwoFactor.keepSignedIn'))
    const policy = wrapper.get('.two-factor-keep__link')
    expect(policy.text()).toBe(t('pages.loginTwoFactor.keepSignedInPolicy'))
    expect(policy.attributes('href')).toBe(router.resolve({ name: 'cookie-policy' }).href)
    expect(policy.attributes('target')).toBe('_blank')

    await keep.setValue(true)
    await wrapper.find('#two-factor-code').setValue('123456')
    await wrapper.find('form').trigger('submit')

    await vi.waitFor(() => expect(bodies).toEqual([{ code: '123456', keepSignedIn: true }]))
  })

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
    expect(wrapper.get('.two-factor-panel a').attributes('href')).toBe(
      router.resolve({ name: 'login', query: { redirect: '/events' } }).href,
    )
  })

  it('sends signed-in users home', async () => {
    useHomeApi()

    const { router } = await renderApp('/login/verify', { user: {} })

    expect(router.currentRoute.value.name).toBe('home')
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
