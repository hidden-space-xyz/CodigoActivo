import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { useUnsubscribe } from '@/pages/unsubscribe/model/use-unsubscribe'

import { mountComposable, t } from '../../../../support/render'
import { apiError, http, noContent, server } from '../../../../support/server'

function serveUnsubscribe(response: () => Response = noContent) {
  const bodies: unknown[] = []
  server.use(
    http.post('/api/emails/unsubscribe', async ({ request }) => {
      bodies.push(await request.json())
      return response()
    }),
  )
  return bodies
}

describe('useUnsubscribe', () => {
  it('sends the token of the link and reports success', async () => {
    const bodies = serveUnsubscribe()
    const { result } = await mountComposable(() => useUnsubscribe())

    result.unsubscribe('token-1')
    expect(result.state.value).toBe('unsubscribing')
    await vi.waitFor(() => expect(result.state.value).toBe('success'))

    expect(bodies).toEqual([{ token: 'token-1' }])
  })

  it('fails a link without token without calling the API', async () => {
    const bodies = serveUnsubscribe()
    const { result } = await mountComposable(() => useUnsubscribe())

    result.unsubscribe(null)
    await flushPromises()

    expect(result.state.value).toBe('error')
    expect(result.errorMessage.value).toBe(t('pages.unsubscribe.incompleteLink'))
    expect(bodies).toHaveLength(0)
  })

  it('shows the localized API error when the link is refused', async () => {
    serveUnsubscribe(() => apiError(400, 'EmailUnsubscribeLinkInvalid'))
    const { result } = await mountComposable(() => useUnsubscribe())

    result.unsubscribe('altered')
    await vi.waitFor(() => expect(result.state.value).toBe('error'))

    expect(result.errorMessage.value).toBe(t('errors.EmailUnsubscribeLinkInvalid'))
  })
})
