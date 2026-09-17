import { describe, expect, it } from 'vitest'

import { forgotPasswordRequest, resetPasswordRequest } from '@/features/auth/api/requests'
import { ApiError } from '@/shared/api'

import { apiError, http, HttpResponse, server, TEST_CSRF_TOKEN } from '../../../../support/server'

describe('auth requests', () => {
  it('posts the email to the forgot-password endpoint with the CSRF token', async () => {
    let received: { body: unknown; csrf: string | null } | undefined
    server.use(
      http.post('/api/auth/forgot-password', async ({ request }) => {
        received = { body: await request.json(), csrf: request.headers.get('X-CSRF-TOKEN') }
        return new HttpResponse(null, { status: 204 })
      }),
    )

    await expect(forgotPasswordRequest('ada@example.test')).resolves.toBeUndefined()

    expect(received).toEqual({ body: { email: 'ada@example.test' }, csrf: TEST_CSRF_TOKEN })
  })

  it('patches the reset-password endpoint of the user with the code and new password', async () => {
    let received: { body: unknown; userId: unknown } | undefined
    server.use(
      http.patch('/api/auth/:userId/reset-password', async ({ request, params }) => {
        received = { body: await request.json(), userId: params.userId }
        return new HttpResponse(null, { status: 204 })
      }),
    )

    await resetPasswordRequest('user-42', '123456', 'a-very-long-password')

    expect(received).toEqual({
      userId: 'user-42',
      body: { otp: '123456', newPassword: 'a-very-long-password' },
    })
  })

  it('rejects with an ApiError when the reset code is rejected', async () => {
    server.use(
      http.patch('/api/auth/:userId/reset-password', () =>
        apiError(400, 'PasswordResetInvalidOrExpired'),
      ),
    )

    const error: unknown = await resetPasswordRequest('user-42', 'bad', 'password').catch(
      (e: unknown) => e,
    )

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).code).toBe('PasswordResetInvalidOrExpired')
  })
})
