import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { useAccountVerification } from '@/features/register'

import { mountComposable } from '../../../../support/fixtures/auth-register/composable'
import { t } from '../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../support/server'

function serveVerify(response: () => Response = () => new HttpResponse(null, { status: 204 })) {
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

describe('useAccountVerification', () => {
  it('starts verifying without a user to resend to', async () => {
    const { result } = await mountComposable(() => useAccountVerification())

    expect(result.state.value).toBe('verifying')
    expect(result.errorMessage.value).toBeNull()
    expect(result.canResend.value).toBe(false)
  })

  it('verifies the link and switches to success', async () => {
    const calls = serveVerify()
    const { result } = await mountComposable(() => useAccountVerification())

    result.verify('user-1', 'otp-1')
    expect(result.state.value).toBe('verifying')
    await vi.waitFor(() => expect(result.state.value).toBe('success'))

    expect(calls).toEqual([{ userId: 'user-1', body: { otp: 'otp-1' } }])
    expect(result.canResend.value).toBe(true)
  })

  it.each([
    { id: null, code: 'otp-1', canResend: false },
    { id: 'user-1', code: null, canResend: true },
  ])(
    'fails an incomplete link (user $id, code $code) without calling the API',
    async ({ id, code, canResend }) => {
      const calls = serveVerify()
      const { result } = await mountComposable(() => useAccountVerification())

      result.verify(id, code)
      await flushPromises()

      expect(result.state.value).toBe('error')
      expect(result.errorMessage.value).toBe(t('features.register.verify.incompleteLink'))
      expect(result.canResend.value).toBe(canResend)
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

  it('does nothing when resending without a user id', async () => {
    const userIds = serveResend()
    const { result } = await mountComposable(() => useAccountVerification())

    result.resend()
    await flushPromises()

    expect(userIds).toHaveLength(0)
  })

  it('resends the link to the user from the verified link and confirms with a toast', async () => {
    serveVerify(() => apiError(400, 'OtpInvalidOrExpired'))
    const userIds = serveResend()
    const { result } = await mountComposable(() => useAccountVerification(), { attach: true })
    result.verify('user-1', 'expired')
    await vi.waitFor(() => expect(result.state.value).toBe('error'))

    result.resend()
    await vi.waitFor(() =>
      expect(notificationText()).toContain(t('features.register.toast.linkResentDetail')),
    )

    expect(userIds).toEqual(['user-1'])
    expect(notificationText()).toContain(t('features.register.toast.linkResentSummary'))
  })

  it('ignores a second resend while the first one is in flight', async () => {
    let release: () => void = () => undefined
    const userIds: unknown[] = []
    server.use(
      http.post('/api/auth/:userId/resend-verification', ({ params }) => {
        userIds.push(params.userId)
        return new Promise<Response>((resolve) => {
          release = () => resolve(new HttpResponse(null, { status: 204 }))
        })
      }),
    )
    const { result } = await mountComposable(() => useAccountVerification())
    result.verify('user-1', null)

    result.resend()
    await vi.waitFor(() => expect(result.isResending.value).toBe(true))
    await vi.waitFor(() => expect(userIds).toHaveLength(1))
    result.resend()
    release()
    await vi.waitFor(() => expect(result.isResending.value).toBe(false))

    expect(userIds).toHaveLength(1)
  })

  it('reports a failed resend as an error toast', async () => {
    serveResend(() => apiError(429, 'OtpResendCooldownActive'))
    const { result } = await mountComposable(() => useAccountVerification(), { attach: true })
    result.verify('user-1', null)

    result.resend()
    await vi.waitFor(() =>
      expect(notificationText()).toContain(t('errors.OtpResendCooldownActive')),
    )
  })
})
