import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { useEmailChangeConfirmation } from '@/pages/confirm-email/model/use-email-change-confirmation'

import { buildUserResponse } from '../../../../support/builders'
import { mountComposable, t } from '../../../../support/render'
import { apiError, http, HttpResponse, noContent, server } from '../../../../support/server'
import { sessionOf } from '../../../../support/session'

const USER_ID = '3f2504e0-4f89-41d3-9a0c-0305e82c3301'

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

    result.confirm(USER_ID, 'code-1')
    expect(result.state.value).toBe('confirming')
    await vi.waitFor(() => expect(result.state.value).toBe('success'))

    expect(calls).toEqual([{ userId: USER_ID, body: { otp: 'code-1' } }])
    await vi.waitFor(() => expect(sessionOf(queryClient).user?.email).toBe('nueva@example.com'))
  })

  it.each([
    { id: null, code: 'code-1' },
    { id: USER_ID, code: null },
    { id: `../users/${USER_ID}/admin?`, code: 'code-1' },
  ])(
    'fails an incomplete or malformed link (user $id, code $code) without calling the API',
    async ({ id, code }) => {
      const calls = serveConfirm()
      const { result } = await mountComposable(() => useEmailChangeConfirmation())

      result.confirm(id, code)
      await flushPromises()

      expect(result.state.value).toBe('error')
      expect(result.errorMessage.value).toBe(t('pages.confirmEmail.incompleteLink'))
      expect(result.staleLink.value).toBe(false)
      expect(calls).toHaveLength(0)
    },
  )

  it('tells that a link refused as spent or expired may be confirmed already', async () => {
    serveConfirm(() => apiError(400, 'OtpInvalidOrExpired'))
    const { result } = await mountComposable(() => useEmailChangeConfirmation())

    result.confirm(USER_ID, 'code-1')

    await vi.waitFor(() => expect(result.state.value).toBe('error'))
    expect(result.staleLink.value).toBe(true)
    expect(result.errorMessage.value).toBe(t('pages.confirmEmail.staleLink'))
  })

  it('shows any other refusal as the API reports it', async () => {
    serveConfirm(() => apiError(409, 'UserEmailAlreadyInUse'))
    const { result } = await mountComposable(() => useEmailChangeConfirmation())

    result.confirm(USER_ID, 'code-1')

    await vi.waitFor(() => expect(result.state.value).toBe('error'))
    expect(result.staleLink.value).toBe(false)
    expect(result.errorMessage.value).toBe(t('errors.UserEmailAlreadyInUse'))
  })
})
