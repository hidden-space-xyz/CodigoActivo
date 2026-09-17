import { describe, expect, it } from 'vitest'

import {
  getCurrentUserRequest,
  getLoginChallengeRequest,
  loginRequest,
  logoutRequest,
  resendTwoFactorCodeRequest,
  verifyTwoFactorLoginRequest,
} from '@/entities/session'
import { ApiError } from '@/shared/api'

import { buildLoginChallenge, buildUserResponse } from '../../../../support/fixtures/user'
import { apiError, http, HttpResponse, server, TEST_CSRF_TOKEN } from '../../../../support/server'

/** Serves a distinct CSRF token per fetch so tests can tell when the cached token was dropped. */
function countCsrfFetches() {
  const state = { count: 0 }
  server.use(
    http.get('/api/auth/csrf', () => {
      state.count += 1
      return HttpResponse.json({ token: `token-${state.count}`, headerName: 'X-CSRF-TOKEN' })
    }),
  )
  return state
}

describe('session requests', () => {
  describe('getCurrentUserRequest', () => {
    it('maps the user from the auth cookie', async () => {
      server.use(http.get('/api/auth/me', () => HttpResponse.json(buildUserResponse())))

      await expect(getCurrentUserRequest()).resolves.toMatchObject({
        id: 'user-1',
        firstName: 'Ada',
        twoFactorMethod: 'Email',
      })
    })

    it('resolves null for the default anonymous session and for 403', async () => {
      await expect(getCurrentUserRequest()).resolves.toBeNull()

      server.use(http.get('/api/auth/me', () => apiError(403)))
      await expect(getCurrentUserRequest()).resolves.toBeNull()
    })

    it('rethrows server errors', async () => {
      server.use(http.get('/api/auth/me', () => apiError(500)))

      await expect(getCurrentUserRequest()).rejects.toBeInstanceOf(ApiError)
    })
  })

  describe('getLoginChallengeRequest', () => {
    it('maps the pending challenge', async () => {
      server.use(
        http.get('/api/auth/login/two-factor', () =>
          HttpResponse.json(buildLoginChallenge({ method: 'Authenticator', maskedEmail: null })),
        ),
      )

      await expect(getLoginChallengeRequest()).resolves.toEqual({
        method: 'Authenticator',
        maskedEmail: null,
      })
    })

    it('resolves null when the challenge expired and rethrows other failures', async () => {
      server.use(
        http.get('/api/auth/login/two-factor', () => apiError(401, 'TwoFactorChallengeExpired')),
      )
      await expect(getLoginChallengeRequest()).resolves.toBeNull()

      server.use(http.get('/api/auth/login/two-factor', () => apiError(500)))
      await expect(getLoginChallengeRequest()).rejects.toBeInstanceOf(ApiError)
    })
  })

  it('runs the password step without touching the CSRF token, then verifies and drops it', async () => {
    const csrf = countCsrfFetches()
    const sent: { url: string; body: unknown; token: string | null }[] = []
    const record = async (request: Request) =>
      sent.push({
        url: new URL(request.url).pathname,
        body: await request.json(),
        token: request.headers.get('X-CSRF-TOKEN'),
      })
    server.use(
      http.post('/api/auth/login', async ({ request }) => {
        await record(request)
        return HttpResponse.json(buildLoginChallenge())
      }),
      http.post('/api/auth/login/two-factor', async ({ request }) => {
        await record(request)
        return HttpResponse.json(buildUserResponse({ firstName: 'Grace' }))
      }),
      http.post('/api/auth/logout', ({ request }) => {
        sent.push({
          url: '/api/auth/logout',
          body: null,
          token: request.headers.get('X-CSRF-TOKEN'),
        })
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const challenge = await loginRequest({ identifier: 'grace@example.test', password: 'secret' })
    const user = await verifyTwoFactorLoginRequest('123456')
    await logoutRequest()

    expect(challenge).toEqual({ method: 'Email', maskedEmail: 'a***@example.test' })
    expect(user.firstName).toBe('Grace')
    expect(sent).toEqual([
      {
        url: '/api/auth/login',
        body: { identifier: 'grace@example.test', password: 'secret' },
        token: 'token-1',
      },
      { url: '/api/auth/login/two-factor', body: { code: '123456' }, token: 'token-1' },
      { url: '/api/auth/logout', body: null, token: 'token-2' },
    ])
    expect(csrf.count).toBe(2)
  })

  it('does not reset the token when the login or the code is rejected', async () => {
    const csrf = countCsrfFetches()
    server.use(
      http.post('/api/auth/login', () => apiError(401, 'InvalidCredentials')),
      http.post('/api/auth/login/two-factor', () => apiError(400, 'TwoFactorCodeInvalid')),
      http.post('/api/auth/logout', () => new HttpResponse(null, { status: 204 })),
    )

    await expect(loginRequest({ identifier: 'x', password: 'y' })).rejects.toMatchObject({
      status: 401,
    })
    await expect(verifyTwoFactorLoginRequest('000000')).rejects.toMatchObject({
      code: 'TwoFactorCodeInvalid',
    })
    await logoutRequest()

    expect(csrf.count).toBe(1)
  })

  it('asks for another code with the CSRF token', async () => {
    let token: string | null = null
    server.use(
      http.post('/api/auth/login/two-factor/resend', ({ request }) => {
        token = request.headers.get('X-CSRF-TOKEN')
        return new HttpResponse(null, { status: 204 })
      }),
    )

    await expect(resendTwoFactorCodeRequest()).resolves.toBeUndefined()

    expect(token).toBe(TEST_CSRF_TOKEN)
  })

  it('resets the CSRF token even when the logout fails', async () => {
    const csrf = countCsrfFetches()
    const tokens: (string | null)[] = []
    let fail = true
    server.use(
      http.post('/api/auth/logout', ({ request }) => {
        tokens.push(request.headers.get('X-CSRF-TOKEN'))
        return fail ? apiError(500) : new HttpResponse(null, { status: 204 })
      }),
    )

    await expect(logoutRequest()).rejects.toBeInstanceOf(ApiError)
    fail = false
    await expect(logoutRequest()).resolves.toBeUndefined()

    expect(tokens).toEqual(['token-1', 'token-2'])
    expect(csrf.count).toBe(2)
  })

  it('uses the default test token when no custom CSRF handler is installed', async () => {
    let token: string | null = null
    server.use(
      http.post('/api/auth/logout', ({ request }) => {
        token = request.headers.get('X-CSRF-TOKEN')
        return new HttpResponse(null, { status: 204 })
      }),
    )

    await logoutRequest()

    expect(token).toBe(TEST_CSRF_TOKEN)
  })
})
