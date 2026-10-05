import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { useEmailChangeConfirmation } from '@/pages/confirm-email/model/use-email-change-confirmation'

import { buildUserResponse } from '../../../../support/builders'
import { mountComposable, t } from '../../../../support/render'
import { apiError, http, HttpResponse, noContent, server } from '../../../../support/server'
import { sessionOf } from '../../../../support/session'

function serveConfirm(response: () => Response = noContent) {
  const calls: { userId: unknown; body: unknown }[] = []
  server.use(
    http.patch('/api/auth/:userId/confirm-email', async ({ request, params }) => {
      calls.push({ userId: params.userId, body: await request.json() })
      return response()
    }),
  )
  return calls
}

describe('useEmailChangeConfirmation', () => {
  it('confirms the link and refreshes the session with the new address', async () => {
    const calls = serveConfirm()
    server.use(
      http.get('/api/auth/me', () =>
        HttpResponse.json(buildUserResponse({ email: 'nueva@example.com' })),
      ),
    )
    const { result, queryClient } = await mountComposable(() => useEmailChangeConfirmation())

    result.confirm('user-1', 'code-1')
    expect(result.state.value).toBe('confirming')
    await vi.waitFor(() => expect(result.state.value).toBe('success'))

    expect(calls).toEqual([{ userId: 'user-1', body: { otp: 'code-1' } }])
    await vi.waitFor(() => expect(sessionOf(queryClient).user?.email).toBe('nueva@example.com'))
  })

  it.each([
    { id: null, code: 'code-1' },
    { id: 'user-1', code: null },
  ])(
    'fails an incomplete link (user $id, code $code) without calling the API',
    async ({ id, code }) => {
      const calls = serveConfirm()
      const { result } = await mountComposable(() => useEmailChangeConfirmation())

      result.confirm(id, code)
      await flushPromises()

      expect(result.state.value).toBe('error')
      expect(result.errorMessage.value).toBe(t('pages.confirmEmail.incompleteLink'))
      expect(calls).toHaveLength(0)
    },
  )

  it('shows the localized API error when the code is rejected', async () => {
    serveConfirm(() => apiError(400, 'OtpInvalidOrExpired'))
    const { result } = await mountComposable(() => useEmailChangeConfirmation())

    result.confirm('user-1', 'expired')
    await vi.waitFor(() => expect(result.state.value).toBe('error'))

    expect(result.errorMessage.value).toBe(t('errors.OtpInvalidOrExpired'))
  })
})
