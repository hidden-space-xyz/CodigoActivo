import { describe, expect, it } from 'vitest'

import { getCurrentUserRequest, loginRequest, logoutRequest } from '@/entities/session'
import { ApiError } from '@/shared/api'

import { buildUserResponse } from '../../../../support/fixtures/user'
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

  it('signs in with the credentials and drops the CSRF token for the next unsafe request', async () => {
    const csrf = countCsrfFetches()
    const sent: { body: unknown; token: string | null }[] = []
    server.use(
      http.post('/api/auth/login', async ({ request }) => {
        sent.push({ body: await request.json(), token: request.headers.get('X-CSRF-TOKEN') })
        return HttpResponse.json(buildUserResponse({ firstName: 'Grace' }))
      }),
      http.post('/api/auth/logout', ({ request }) => {
        sent.push({ body: null, token: request.headers.get('X-CSRF-TOKEN') })
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const user = await loginRequest({ identifier: 'grace@example.test', password: 'secret' })
    await logoutRequest()

    expect(user.firstName).toBe('Grace')
    expect(sent).toEqual([
      { body: { identifier: 'grace@example.test', password: 'secret' }, token: 'token-1' },
      { body: null, token: 'token-2' },
    ])
    expect(csrf.count).toBe(2)
  })

  it('does not reset the token when the login is rejected', async () => {
    const csrf = countCsrfFetches()
    server.use(
      http.post('/api/auth/login', () => apiError(401, 'InvalidCredentials')),
      http.post('/api/auth/logout', () => new HttpResponse(null, { status: 204 })),
    )

    await expect(loginRequest({ identifier: 'x', password: 'y' })).rejects.toMatchObject({
      status: 401,
    })
    await logoutRequest()

    expect(csrf.count).toBe(1)
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
