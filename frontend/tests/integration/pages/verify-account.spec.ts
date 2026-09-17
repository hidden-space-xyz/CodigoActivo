import { describe, expect, it, vi } from 'vitest'

import { renderApp, t } from '../../support/render'
import { apiError, http, HttpResponse, server } from '../../support/server'

function serveVerify(response: () => Response | Promise<Response>) {
  const calls: { userId: unknown; body: unknown }[] = []
  server.use(
    http.patch('/api/auth/:userId/verify', async ({ request, params }) => {
      calls.push({ userId: params.userId, body: await request.json() })
      return response()
    }),
  )
  return calls
}

function serveResend(response: () => Response = () => new HttpResponse(null, { status: 204 })) {
  const userIds: unknown[] = []
  server.use(
    http.post('/api/auth/:userId/resend-verification', ({ params }) => {
      userIds.push(params.userId)
      return response()
    }),
  )
  return userIds
}

function notificationText(): string {
  return document.body.querySelector('.el-notification')?.textContent ?? ''
}

function resendButton(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper']) {
  return wrapper
    .get('main')
    .findAll('button')
    .find((button) => button.text() === t('pages.verifyAccount.resend'))
}

describe('verify account page', () => {
  it('shows progress while verifying and then the success panel', async () => {
    let release: () => void = () => undefined
    const calls = serveVerify(
      () =>
        new Promise<Response>((resolve) => {
          release = () => resolve(new HttpResponse(null, { status: 204 }))
        }),
    )

    const { wrapper, router } = await renderApp('/verify-account?userId=user-1&code=otp-1')

    await vi.waitFor(() => expect(calls).toHaveLength(1))
    expect(wrapper.get('.verify-card').classes()).toContain('verify-card--verifying')
    expect(wrapper.text()).toContain(t('pages.verifyAccount.verifying'))

    release()

    await vi.waitFor(() => expect(wrapper.text()).toContain(t('pages.verifyAccount.successTitle')))
    expect(calls).toEqual([{ userId: 'user-1', body: { otp: 'otp-1' } }])
    expect(wrapper.get('.verify-card a').attributes('href')).toBe(
      router.resolve({ name: 'login' }).href,
    )
  })

  it('reports an incomplete link without calling the API or offering a resend', async () => {
    const calls = serveVerify(() => new HttpResponse(null, { status: 204 }))

    const { wrapper } = await renderApp('/verify-account?code=otp-1')

    expect(wrapper.text()).toContain(t('pages.verifyAccount.errorTitle'))
    expect(wrapper.get('[role="alert"]').text()).toBe(t('features.register.verify.incompleteLink'))
    expect(resendButton(wrapper)).toBeUndefined()
    expect(calls).toHaveLength(0)
  })

  it('shows the API error for an expired link and resends a new one', async () => {
    serveVerify(() => apiError(400, 'OtpInvalidOrExpired'))
    const userIds = serveResend()

    const { wrapper } = await renderApp('/verify-account?userId=user-1&code=expired')

    await vi.waitFor(() =>
      expect(wrapper.find('[role="alert"]').text()).toBe(t('errors.OtpInvalidOrExpired')),
    )
    expect(wrapper.text()).toContain(t('pages.verifyAccount.hint'))

    await resendButton(wrapper)?.trigger('click')

    await vi.waitFor(() =>
      expect(notificationText()).toContain(t('features.register.toast.linkResentDetail')),
    )
    expect(userIds).toEqual(['user-1'])
  })

  it('offers a resend for a link that has a user but no code, reporting resend failures', async () => {
    serveResend(() => apiError(429, 'OtpResendCooldownActive'))

    const { wrapper } = await renderApp('/verify-account?userId=user-1&userId=user-2&code=')

    expect(wrapper.get('[role="alert"]').text()).toBe(t('features.register.verify.incompleteLink'))
    await resendButton(wrapper)?.trigger('click')

    await vi.waitFor(() =>
      expect(notificationText()).toContain(t('errors.OtpResendCooldownActive')),
    )
  })
})
