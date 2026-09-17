import type { VueWrapper } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { renderApp, t } from '../../support/render'
import { apiError, http, HttpResponse, server } from '../../support/server'

const USER_ID = '3f2504e0-4f89-41d3-9a0c-0305e82c3301'
const PASSWORD = 'correct-horse-battery'

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

async function fillPasswords(wrapper: VueWrapper, password: string, confirm = password) {
  await wrapper.find('#reset-password').setValue(password)
  await wrapper.find('#reset-password-confirm').setValue(confirm)
  await wrapper.find('form').trigger('submit')
}

describe('reset password page', () => {
  it.each([
    '/reset-password',
    `/reset-password?userId=${USER_ID}`,
    `/reset-password?userId=${USER_ID}&code=`,
    '/reset-password?userId=not-a-guid&code=abc',
  ])('shows an invalid link panel for %s', async (path) => {
    const { wrapper, router } = await renderApp(path)

    expect(wrapper.text()).toContain(t('pages.resetPassword.invalidTitle'))
    expect(wrapper.find('form').exists()).toBe(false)
    const hrefs = wrapper.findAll('.reset-panel__actions a').map((a) => a.attributes('href'))
    expect(hrefs).toEqual([
      router.resolve({ name: 'forgot-password' }).href,
      router.resolve({ name: 'home' }).href,
    ])
  })

  it('validates the password locally before calling the API', async () => {
    const calls = serveReset()
    const { wrapper } = await renderApp(`/reset-password?userId=${USER_ID}&code=otp-1`)

    await fillPasswords(wrapper, 'too-short')
    expect(wrapper.get('[role="alert"]').text()).toBe(t('validation.newPasswordMin'))

    await fillPasswords(wrapper, PASSWORD, `${PASSWORD}?`)
    expect(wrapper.get('[role="alert"]').text()).toBe(t('validation.passwordsMismatch'))

    expect(calls).toHaveLength(0)
  })

  it('changes the password and offers to log in', async () => {
    const calls = serveReset()
    const { wrapper, router } = await renderApp(`/reset-password?userId=${USER_ID}&code=otp-1`)

    await fillPasswords(wrapper, PASSWORD)

    await vi.waitFor(() => expect(wrapper.text()).toContain(t('pages.resetPassword.successTitle')))
    expect(calls).toEqual([{ userId: USER_ID, body: { otp: 'otp-1', newPassword: PASSWORD } }])
    expect(wrapper.get('.reset-panel__actions a').attributes('href')).toBe(
      router.resolve({ name: 'login' }).href,
    )
  })

  it('uses the first value when a query parameter is repeated', async () => {
    const calls = serveReset()
    const { wrapper } = await renderApp(
      `/reset-password?userId=${USER_ID}&userId=other&code=first&code=second`,
    )

    await fillPasswords(wrapper, PASSWORD)

    await vi.waitFor(() => expect(calls).toHaveLength(1))
    expect(calls[0]).toEqual({ userId: USER_ID, body: { otp: 'first', newPassword: PASSWORD } })
  })

  it('shows the API error and a link to request a new reset email', async () => {
    serveReset(() => apiError(400, 'PasswordResetInvalidOrExpired'))
    const { wrapper, router } = await renderApp(`/reset-password?userId=${USER_ID}&code=old`)

    await fillPasswords(wrapper, PASSWORD)

    await vi.waitFor(() => expect(wrapper.find('.reset-request-link').exists()).toBe(true))
    expect(wrapper.get('[role="alert"]').text()).toBe(t('errors.PasswordResetInvalidOrExpired'))
    expect(wrapper.get('.reset-request-link').attributes('href')).toBe(
      router.resolve({ name: 'forgot-password' }).href,
    )
  })
})
