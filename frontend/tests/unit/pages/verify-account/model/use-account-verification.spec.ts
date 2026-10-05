import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { useAccountVerification } from '@/pages/verify-account/model/use-account-verification'

import { t } from '../../../../support/render'
import { apiError, http, noContent, server } from '../../../../support/server'
import { mountComposable } from '../../../../support/render'

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

function notificationText(): string {
  return document.body.querySelector('.el-notification')?.textContent ?? ''
}

describe('useAccountVerification', () => {
  it('starts verifying with an empty resend form', async () => {
    const { result } = await mountComposable(() => useAccountVerification())

    expect(result.state.value).toBe('verifying')
    expect(result.errorMessage.value).toBeNull()
    expect(result.resendForm.email).toBe('')
  })

  it('verifies the link and switches to success', async () => {
    const calls = serveVerify()
    const { result } = await mountComposable(() => useAccountVerification())

    result.verify('user-1', 'otp-1')
    expect(result.state.value).toBe('verifying')
    await vi.waitFor(() => expect(result.state.value).toBe('success'))

    expect(calls).toEqual([{ userId: 'user-1', body: { otp: 'otp-1' } }])
  })

  it.each([
    { id: null, code: 'otp-1' },
    { id: 'user-1', code: null },
  ])(
    'fails an incomplete link (user $id, code $code) without calling the API',
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

  it('shows the localized API error when the code is rejected', async () => {
    serveVerify(() => apiError(400, 'OtpInvalidOrExpired'))
    const { result } = await mountComposable(() => useAccountVerification())

    result.verify('user-1', 'expired')
    await vi.waitFor(() => expect(result.state.value).toBe('error'))

    expect(result.errorMessage.value).toBe(t('errors.OtpInvalidOrExpired'))
  })

  it('asks for a new link for the trimmed address and confirms with a neutral toast', async () => {
    const bodies = serveResend()
    const { result } = await mountComposable(() => useAccountVerification(), { attach: true })
    result.resendForm.email = '  ada@example.test '

    result.resend()
    await vi.waitFor(() =>
      expect(notificationText()).toContain(t('entities.account.verification.linkResentDetail')),
    )

    expect(bodies).toEqual([{ email: 'ada@example.test' }])
    expect(notificationText()).toContain(t('entities.account.verification.linkResentSummary'))
  })

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

  it('reports a failed resend as an error toast', async () => {
    serveResend(() => apiError(400, 'RequestValidationFailed'))
    const { result } = await mountComposable(() => useAccountVerification(), { attach: true })
    result.resendForm.email = 'not-an-email'

    result.resend()
    await vi.waitFor(() =>
      expect(notificationText()).toContain(t('errors.RequestValidationFailed')),
    )
  })
})
