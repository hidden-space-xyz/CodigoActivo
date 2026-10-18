import { describe, expect, it, vi } from 'vitest'

import { renderApp, t } from '../../../../support/render'
import {
  apiError,
  http,
  HttpResponse,
  noContent,
  server,
  TEST_LEGAL_ENTITY,
} from '../../../../support/server'

function serveUnsubscribe(response: () => Response | Promise<Response> = noContent) {
  const bodies: unknown[] = []
  server.use(
    http.post('/api/emails/unsubscribe', async ({ request }) => {
      bodies.push(await request.json())
      return response()
    }),
  )
  return bodies
}

describe('unsubscribe page', () => {
  it('unsubscribes without a session as soon as the link opens and hides the token', async () => {
    let release: () => void = () => undefined
    const bodies = serveUnsubscribe(
      () =>
        new Promise<Response>((resolve) => {
          release = () => resolve(new HttpResponse(null, { status: 204 }))
        }),
    )

    const { wrapper, router } = await renderApp('/unsubscribe#token=token-1')

    await vi.waitFor(() => expect(bodies).toHaveLength(1))
    await vi.waitFor(() => expect(router.currentRoute.value.fullPath).toBe('/unsubscribe'))
    expect(wrapper.get('.status-panel').classes()).toContain('status-panel--pending')
    expect(wrapper.text()).toContain(t('pages.unsubscribe.unsubscribing'))

    release()

    await vi.waitFor(() => expect(wrapper.text()).toContain(t('pages.unsubscribe.successTitle')))
    expect(wrapper.text()).toContain(t('pages.unsubscribe.successText'))
    expect(bodies).toEqual([{ token: 'token-1' }])
    expect(wrapper.get('.status-panel a').attributes('href')).toBe(
      router.resolve({ name: 'home' }).href,
    )
  })

  it('reports a link without token and offers the other ways out', async () => {
    const bodies = serveUnsubscribe()

    const { wrapper } = await renderApp('/unsubscribe')

    expect(wrapper.text()).toContain(t('pages.unsubscribe.errorTitle'))
    expect(wrapper.get('[role="alert"]').text()).toBe(t('pages.unsubscribe.incompleteLink'))
    await vi.waitFor(() =>
      expect(wrapper.text()).toContain(
        t('pages.unsubscribe.hint', { email: TEST_LEGAL_ENTITY.email }),
      ),
    )
    expect(bodies).toHaveLength(0)
  })

  it('shows the API error for an altered link', async () => {
    serveUnsubscribe(() => apiError(400, 'EmailUnsubscribeLinkInvalid'))

    const { wrapper } = await renderApp('/unsubscribe#token=altered')

    await vi.waitFor(() =>
      expect(wrapper.find('[role="alert"]').text()).toBe(t('errors.EmailUnsubscribeLinkInvalid')),
    )
  })
})
