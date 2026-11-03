import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { useAccountVerification } from '@/pages/verify-account/model/use-account-verification'

import { t } from '../../../../support/render'
import { http, noContent, server } from '../../../../support/server'
import { mountComposable } from '../../../../support/render'

const USER_ID = '3f2504e0-4f89-41d3-9a0c-0305e82c3301'

function serveVerify(response: () => Response = noContent) {
  const calls: { userId: unknown; body: unknown }[] = []
  server.use(
    http.patch('/api/auth/:userId/verify', async ({ request, params }) => {
      calls.push({ userId: params.userId, body: await request.json() })
      return response()
    }),
  )
  return calls
}

function serveResend(response: () => Response | Promise<Response> = noContent) {
  const bodies: unknown[] = []
  server.use(
    http.post('/api/auth/resend-verification', async ({ request }) => {
      bodies.push(await request.json())
      return response()
    }),
  )
  return bodies
}

describe('useAccountVerification', () => {
  it.each([
    { id: null, code: 'otp-1' },
    { id: USER_ID, code: null },
    { id: `../users/${USER_ID}/admin?`, code: 'otp-1' },
  ])(
    'fails an incomplete or malformed link (user $id, code $code) without calling the API',
    async ({ id, code }) => {
      const calls = serveVerify()
      const { result } = await mountComposable(() => useAccountVerification())

      result.verify(id, code)
      await flushPromises()

      expect(result.state.value).toBe('error')
      expect(result.errorMessage.value).toBe(t('pages.verifyAccount.incompleteLink'))
      expect(calls).toHaveLength(0)
    },
  )

  it('ignores a second resend while the first one is in flight', async () => {
    let release: () => void = () => undefined
    const bodies = serveResend(
      () =>
        new Promise<Response>((resolve) => {
          release = () => resolve(noContent())
        }),
    )
    const { result } = await mountComposable(() => useAccountVerification())
    result.resendForm.email = 'ada@example.test'

    result.resend()
    await vi.waitFor(() => expect(result.isResending.value).toBe(true))
    await vi.waitFor(() => expect(bodies).toHaveLength(1))
    result.resend()
    release()
    await vi.waitFor(() => expect(result.isResending.value).toBe(false))

    expect(bodies).toHaveLength(1)
  })
})
