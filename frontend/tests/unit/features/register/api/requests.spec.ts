import { describe, expect, it } from 'vitest'

import {
  registerRequest,
  resendVerificationRequest,
  verifyRegistrationRequest,
} from '@/features/register/api/requests'
import { createEmptyRegistrationForm } from '@/features/register/model/registration-form'
import type { RegisterResponse } from '@/shared/api/generated/models'
import { ApiError } from '@/shared/api'

import { buildUserResponse } from '../../../../support/fixtures/user'
import { apiError, http, HttpResponse, server, TEST_CSRF_TOKEN } from '../../../../support/server'

describe('register requests', () => {
  it('posts the mapped registration and returns the reduced result', async () => {
    let received: { body: unknown; csrf: string | null } | undefined
    server.use(
      http.post('/api/auth/register', async ({ request }) => {
        received = { body: await request.json(), csrf: request.headers.get('X-CSRF-TOKEN') }
        const response: RegisterResponse = {
          adult: buildUserResponse({ id: 'adult-1' }),
          minors: [],
          requiresVerification: true,
        }
        return HttpResponse.json(response, { status: 201 })
      }),
    )
    const form = {
      ...createEmptyRegistrationForm(),
      firstName: 'Ada',
      lastName: 'Lovelace',
      email: 'ada@example.test',
      phone: '600000000',
      password: 'correct-horse-battery',
      confirmPassword: 'correct-horse-battery',
      dateOfBirth: '1990-05-10',
      gender: 'Female' as const,
    }

    await expect(registerRequest(form)).resolves.toEqual({
      adultId: 'adult-1',
      requiresVerification: true,
      minorCount: 0,
    })
    expect(received).toEqual({
      csrf: TEST_CSRF_TOKEN,
      body: {
        firstName: 'Ada',
        lastName: 'Lovelace',
        email: 'ada@example.test',
        phone: '600000000',
        password: 'correct-horse-battery',
        birthDate: '1990-05-10',
        gender: 'Female',
        minors: [],
      },
    })
  })

  it('patches the verify endpoint of the user with the one-time code', async () => {
    let received: { userId: unknown; body: unknown } | undefined
    server.use(
      http.patch('/api/auth/:userId/verify', async ({ request, params }) => {
        received = { userId: params.userId, body: await request.json() }
        return new HttpResponse(null, { status: 204 })
      }),
    )

    await verifyRegistrationRequest('user-7', '654321')

    expect(received).toEqual({ userId: 'user-7', body: { otp: '654321' } })
  })

  it('posts to the resend-verification endpoint of the user', async () => {
    const userIds: unknown[] = []
    server.use(
      http.post('/api/auth/:userId/resend-verification', ({ params }) => {
        userIds.push(params.userId)
        return new HttpResponse(null, { status: 204 })
      }),
    )

    await resendVerificationRequest('user-7')

    expect(userIds).toEqual(['user-7'])
  })

  it('rejects with the API error code when verification fails', async () => {
    server.use(http.patch('/api/auth/:userId/verify', () => apiError(400, 'OtpInvalidOrExpired')))

    const error: unknown = await verifyRegistrationRequest('user-7', 'bad').catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).code).toBe('OtpInvalidOrExpired')
  })
})
