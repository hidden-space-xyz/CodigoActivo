import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { useResetPassword } from '@/features/auth'

import { mountComposable } from '../../../../support/fixtures/auth-register/composable'
import { t } from '../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../support/server'

const USER_ID = '3f2504e0-4f89-41d3-9a0c-0305e82c3301'
const VALID_PASSWORD = 'correct-horse-battery'

function serveReset(response: () => Response = () => new HttpResponse(null, { status: 204 })) {
  const calls: { userId: unknown; body: unknown }[] = []
  server.use(
    http.patch('/api/auth/:userId/reset-password', async ({ request, params }) => {
      calls.push({ userId: params.userId, body: await request.json() })
      return response()
    }),
  )
  return calls
}

describe('useResetPassword', () => {
  it.each([
    { userId: USER_ID, code: 'abc', valid: true },
    { userId: USER_ID.toUpperCase(), code: 'abc', valid: true },
    { userId: 'not-a-guid', code: 'abc', valid: false },
    { userId: null, code: 'abc', valid: false },
    { userId: USER_ID, code: null, valid: false },
  ])('reports link validity $valid for user $userId and code $code', async (link) => {
    const { result } = await mountComposable(() => useResetPassword(link.userId, link.code))

    expect(result.hasValidLink).toBe(link.valid)
    expect(result.state.value).toBe('form')
  })

  it('rejects passwords shorter than 12 characters without calling the API', async () => {
    const calls = serveReset()
    const { result } = await mountComposable(() => useResetPassword(USER_ID, 'abc'))
    result.form.password = 'short'
    result.form.confirmPassword = 'short'

    result.submit()
    await flushPromises()

    expect(result.errorMessage.value).toBe(t('validation.newPasswordMin'))
    expect(calls).toHaveLength(0)
  })

  it('rejects a confirmation that does not match without calling the API', async () => {
    const calls = serveReset()
    const { result } = await mountComposable(() => useResetPassword(USER_ID, 'abc'))
    result.form.password = VALID_PASSWORD
    result.form.confirmPassword = `${VALID_PASSWORD}!`

    result.submit()
    await flushPromises()

    expect(result.errorMessage.value).toBe(t('validation.passwordsMismatch'))
    expect(calls).toHaveLength(0)
  })

  it('does not call the API when the link has no code', async () => {
    const calls = serveReset()
    const { result } = await mountComposable(() => useResetPassword(USER_ID, null))
    result.form.password = VALID_PASSWORD
    result.form.confirmPassword = VALID_PASSWORD

    result.submit()
    await flushPromises()

    expect(calls).toHaveLength(0)
    expect(result.errorMessage.value).toBeNull()
    expect(result.state.value).toBe('form')
  })

  it('resets the password and switches to the success state', async () => {
    const calls = serveReset()
    const { result } = await mountComposable(() => useResetPassword(USER_ID, 'otp-1'))
    result.form.password = VALID_PASSWORD
    result.form.confirmPassword = VALID_PASSWORD

    result.submit()
    await vi.waitFor(() => expect(result.state.value).toBe('success'))

    expect(calls).toEqual([
      { userId: USER_ID, body: { otp: 'otp-1', newPassword: VALID_PASSWORD } },
    ])
    expect(result.errorMessage.value).toBeNull()
    expect(result.canRequestNewLink.value).toBe(false)
  })

  it('shows the localized API error and offers a new link, clearing both on retry', async () => {
    serveReset(() => apiError(400, 'PasswordResetInvalidOrExpired'))
    const { result } = await mountComposable(() => useResetPassword(USER_ID, 'otp-1'))
    result.form.password = VALID_PASSWORD
    result.form.confirmPassword = VALID_PASSWORD

    result.submit()
    await vi.waitFor(() => expect(result.canRequestNewLink.value).toBe(true))
    expect(result.errorMessage.value).toBe(t('errors.PasswordResetInvalidOrExpired'))
    expect(result.state.value).toBe('form')

    result.form.password = 'short'
    result.submit()

    expect(result.canRequestNewLink.value).toBe(false)
    expect(result.errorMessage.value).toBe(t('validation.newPasswordMin'))
  })
})
