import { describe, expect, it, vi } from 'vitest'

import { buildUserResponse } from '../../../../support/builders'
import { renderApp, t } from '../../../../support/render'
import { apiError, http, HttpResponse, noContent, server } from '../../../../support/server'

function serveConfirm(response: () => Response | Promise<Response> = noContent) {
  const calls: { userId: unknown; body: unknown }[] = []
  server.use(
    http.patch('/api/auth/:userId/confirm-email', async ({ request, params }) => {
      calls.push({ userId: params.userId, body: await request.json() })
      return response()
    }),
  )
  return calls
}

describe('confirm email page', () => {
  it('shows progress while confirming and then sends a guest to log in', async () => {
    let release: () => void = () => undefined
    const calls = serveConfirm(
      () =>
        new Promise<Response>((resolve) => {
          release = () => resolve(new HttpResponse(null, { status: 204 }))
        }),
    )

    const { wrapper, router } = await renderApp('/confirm-email#userId=user-1&code=code-1')

    await vi.waitFor(() => expect(calls).toHaveLength(1))
    await vi.waitFor(() => expect(router.currentRoute.value.fullPath).toBe('/confirm-email'))
    expect(wrapper.get('.status-panel').classes()).toContain('status-panel--pending')
    expect(wrapper.text()).toContain(t('pages.confirmEmail.confirming'))

    release()

    await vi.waitFor(() => expect(wrapper.text()).toContain(t('pages.confirmEmail.successTitle')))
    expect(calls).toEqual([{ userId: 'user-1', body: { otp: 'code-1' } }])
    expect(wrapper.get('.status-panel a').attributes('href')).toBe(
      router.resolve({ name: 'login' }).href,
    )
  })

  it('sends a signed-in holder back to their account', async () => {
    serveConfirm()
    server.use(http.get('/api/auth/me', () => HttpResponse.json(buildUserResponse())))

    const { wrapper, router } = await renderApp('/confirm-email#userId=user-1&code=code-1', {
      user: {},
    })

    await vi.waitFor(() => expect(wrapper.text()).toContain(t('pages.confirmEmail.successTitle')))
    expect(wrapper.get('.status-panel a').attributes('href')).toBe(
      router.resolve({ name: 'account' }).href,
    )
  })

  it('reports an incomplete link without calling the API', async () => {
    const calls = serveConfirm()

    const { wrapper } = await renderApp('/confirm-email#code=code-1')

    expect(wrapper.text()).toContain(t('pages.confirmEmail.errorTitle'))
    expect(wrapper.get('[role="alert"]').text()).toBe(t('pages.confirmEmail.incompleteLink'))
    expect(wrapper.text()).toContain(t('pages.confirmEmail.hint'))
    expect(calls).toHaveLength(0)
  })

  it('shows the API error for an expired link', async () => {
    serveConfirm(() => apiError(400, 'OtpInvalidOrExpired'))

    const { wrapper } = await renderApp('/confirm-email#userId=user-1&code=expired')

    await vi.waitFor(() =>
      expect(wrapper.find('[role="alert"]').text()).toBe(t('errors.OtpInvalidOrExpired')),
    )
  })
})
