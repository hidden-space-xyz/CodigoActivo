import { describe, expect, it, vi } from 'vitest'

import { renderApp, t } from '../../../../support/render'
import { apiError, http, HttpResponse, noContent, server } from '../../../../support/server'

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

function serveResend(response: () => Response = noContent) {
  const bodies: unknown[] = []
  server.use(
    http.post('/api/auth/resend-verification', async ({ request }) => {
      bodies.push(await request.json())
      return response()
    }),
  )
  return bodies
}

function notificationText(): string {
  return document.body.querySelector('.el-notification')?.textContent ?? ''
}

async function requestNewLink(
  wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper'],
  email: string,
): Promise<void> {
  await wrapper.get('#verify-resend-email').setValue(email)
  await wrapper.get('form.verify-resend').trigger('submit')
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

    const { wrapper, router } = await renderApp('/verify-account#userId=user-1&code=otp-1')

    await vi.waitFor(() => expect(calls).toHaveLength(1))
    await vi.waitFor(() => expect(router.currentRoute.value.fullPath).toBe('/verify-account'))
    expect(wrapper.get('.status-panel').classes()).toContain('status-panel--pending')
    expect(wrapper.text()).toContain(t('pages.verifyAccount.verifying'))

    release()

    await vi.waitFor(() => expect(wrapper.text()).toContain(t('pages.verifyAccount.successTitle')))
    expect(calls).toEqual([{ userId: 'user-1', body: { otp: 'otp-1' } }])
    expect(wrapper.get('.status-panel a').attributes('href')).toBe(
      router.resolve({ name: 'login' }).href,
    )
  })

  it('reports an incomplete link without calling the API and offers a new link by email', async () => {
    const calls = serveVerify(noContent)
    const bodies = serveResend()

    const { wrapper } = await renderApp('/verify-account#code=otp-1')

    expect(wrapper.text()).toContain(t('pages.verifyAccount.errorTitle'))
    expect(wrapper.get('[role="alert"]').text()).toBe(t('pages.verifyAccount.incompleteLink'))
    expect(wrapper.text()).toContain(t('pages.verifyAccount.hint'))
    expect(calls).toHaveLength(0)

    await requestNewLink(wrapper, ' ada@example.test ')

    await vi.waitFor(() =>
      expect(notificationText()).toContain(t('entities.account.verification.linkResentDetail')),
    )
    expect(bodies).toEqual([{ email: 'ada@example.test' }])
  })

  it('shows the API error for an expired link and reports resend failures', async () => {
    serveVerify(() => apiError(400, 'OtpInvalidOrExpired'))
    serveResend(() => apiError(400, 'RequestValidationFailed'))

    const { wrapper } = await renderApp('/verify-account#userId=user-1&code=expired')

    await vi.waitFor(() =>
      expect(wrapper.find('[role="alert"]').text()).toBe(t('errors.OtpInvalidOrExpired')),
    )

    await requestNewLink(wrapper, 'ada@example.test')

    await vi.waitFor(() =>
      expect(notificationText()).toContain(t('errors.RequestValidationFailed')),
    )
  })
})
