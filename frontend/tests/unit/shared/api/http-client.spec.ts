import { describe, expect, it } from 'vitest'

import { ApiError, httpClient, resetCsrfToken } from '@/shared/api'
import { ErrorCode } from '@/shared/api/generated/models'

import { apiError, http, HttpResponse, server, TEST_CSRF_TOKEN } from '../../../support/server'

interface ClientResult {
  status: number
  data: unknown
  headers: Headers
}

function capture(error: unknown): ApiError {
  if (!(error instanceof ApiError)) throw new Error('Expected an ApiError')
  return error
}

async function failure(promise: Promise<unknown>): Promise<ApiError> {
  try {
    await promise
  } catch (error) {
    return capture(error)
  }
  throw new Error('Expected the request to fail')
}

describe('httpClient', () => {
  it('sends GET requests with a JSON accept header and no CSRF token', async () => {
    let csrfCalls = 0
    let seen: { accept: string | null; csrf: string | null } | undefined
    server.use(
      http.get('/api/auth/csrf', () => {
        csrfCalls += 1
        return HttpResponse.json({ token: TEST_CSRF_TOKEN })
      }),
      http.get('/api/things', ({ request }) => {
        seen = {
          accept: request.headers.get('Accept'),
          csrf: request.headers.get('X-CSRF-TOKEN'),
        }
        return HttpResponse.json({ id: 1 })
      }),
    )

    const result = await httpClient<ClientResult>('/api/things')

    expect(result.status).toBe(200)
    expect(result.data).toEqual({ id: 1 })
    expect(result.headers.get('content-type')).toContain('json')
    expect(seen).toEqual({ accept: 'application/json', csrf: null })
    expect(csrfCalls).toBe(0)
  })

  it('keeps a caller-provided accept header', async () => {
    let accept: string | null = null
    server.use(
      http.get('/api/export', ({ request }) => {
        accept = request.headers.get('Accept')
        return new HttpResponse('a;b', { headers: { 'Content-Type': 'text/csv' } })
      }),
    )

    const result = await httpClient<ClientResult>('/api/export', {
      headers: { Accept: 'text/csv' },
    })

    expect(accept).toBe('text/csv')
    expect(result.data).toBe('a;b')
  })

  it('attaches the CSRF token to unsafe requests and caches it between requests', async () => {
    let csrfCalls = 0
    const tokens: (string | null)[] = []
    server.use(
      http.get('/api/auth/csrf', () => {
        csrfCalls += 1
        return HttpResponse.json({ token: TEST_CSRF_TOKEN, headerName: 'X-CSRF-TOKEN' })
      }),
      http.post('/api/things', ({ request }) => {
        tokens.push(request.headers.get('X-CSRF-TOKEN'))
        return HttpResponse.json({ ok: true }, { status: 201 })
      }),
      http.delete('/api/things/1', ({ request }) => {
        tokens.push(request.headers.get('X-CSRF-TOKEN'))
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const created = await httpClient<ClientResult>('/api/things', { method: 'post' })
    const deleted = await httpClient<ClientResult>('/api/things/1', { method: 'DELETE' })

    expect(created).toMatchObject({ status: 201, data: { ok: true } })
    expect(deleted).toMatchObject({ status: 204, data: undefined })
    expect(tokens).toEqual([TEST_CSRF_TOKEN, TEST_CSRF_TOKEN])
    expect(csrfCalls).toBe(1)
  })

  it('shares one CSRF token request between concurrent unsafe requests', async () => {
    let csrfCalls = 0
    server.use(
      http.get('/api/auth/csrf', async () => {
        csrfCalls += 1
        await new Promise((resolve) => setTimeout(resolve, 10))
        return HttpResponse.json({ token: TEST_CSRF_TOKEN })
      }),
      http.put('/api/things/:id', ({ request }) =>
        HttpResponse.json({ csrf: request.headers.get('X-CSRF-TOKEN') }),
      ),
    )

    const results = await Promise.all([
      httpClient<ClientResult>('/api/things/1', { method: 'PUT' }),
      httpClient<ClientResult>('/api/things/2', { method: 'PUT' }),
    ])

    expect(csrfCalls).toBe(1)
    expect(results.map((result) => result.data)).toEqual([
      { csrf: TEST_CSRF_TOKEN },
      { csrf: TEST_CSRF_TOKEN },
    ])
  })

  it('uses the header name announced by the CSRF endpoint', async () => {
    let custom: string | null = null
    server.use(
      http.get('/api/auth/csrf', () =>
        HttpResponse.json({ token: 'custom-token', headerName: 'X-Custom-Csrf' }),
      ),
      http.patch('/api/things/1', ({ request }) => {
        custom = request.headers.get('X-Custom-Csrf')
        return HttpResponse.json({})
      }),
    )

    await httpClient('/api/things/1', { method: 'PATCH' })

    expect(custom).toBe('custom-token')
  })

  it('fetches a new token after the cached one is reset', async () => {
    let csrfCalls = 0
    server.use(
      http.get('/api/auth/csrf', () => {
        csrfCalls += 1
        return HttpResponse.json({ token: `token-${csrfCalls}`, headerName: 'X-CSRF-TOKEN' })
      }),
      http.post('/api/things', ({ request }) =>
        HttpResponse.json({ csrf: request.headers.get('X-CSRF-TOKEN') }),
      ),
    )

    const first = await httpClient<ClientResult>('/api/things', { method: 'POST' })
    resetCsrfToken()
    const second = await httpClient<ClientResult>('/api/things', { method: 'POST' })

    expect(first.data).toEqual({ csrf: 'token-1' })
    expect(second.data).toEqual({ csrf: 'token-2' })
  })

  it('sends unsafe requests without a token when the CSRF endpoint fails', async () => {
    let csrf: string | null = 'unset'
    server.use(
      http.get('/api/auth/csrf', () => apiError(500)),
      http.post('/api/things', ({ request }) => {
        csrf = request.headers.get('X-CSRF-TOKEN')
        return HttpResponse.json({})
      }),
    )

    await httpClient('/api/things', { method: 'POST' })

    expect(csrf).toBeNull()
  })

  it('sends unsafe requests without a token when the CSRF response has none', async () => {
    let csrf: string | null = 'unset'
    server.use(
      http.get('/api/auth/csrf', () => HttpResponse.json({})),
      http.post('/api/things', ({ request }) => {
        csrf = request.headers.get('X-CSRF-TOKEN')
        return HttpResponse.json({})
      }),
    )

    await httpClient('/api/things', { method: 'POST' })

    expect(csrf).toBeNull()
  })

  it('retries an unsafe request once with a fresh token on InvalidCsrfToken', async () => {
    let csrfCalls = 0
    const sent: (string | null)[] = []
    server.use(
      http.get('/api/auth/csrf', () => {
        csrfCalls += 1
        return HttpResponse.json({ token: `token-${csrfCalls}`, headerName: 'X-CSRF-TOKEN' })
      }),
      http.post('/api/things', ({ request }) => {
        sent.push(request.headers.get('X-CSRF-TOKEN'))
        if (sent.length === 1) return apiError(400, ErrorCode.InvalidCsrfToken)
        return HttpResponse.json({ saved: true })
      }),
    )

    const result = await httpClient<ClientResult>('/api/things', { method: 'POST' })

    expect(result.data).toEqual({ saved: true })
    expect(sent).toEqual(['token-1', 'token-2'])
  })

  it('gives up after the retry also fails with InvalidCsrfToken', async () => {
    let attempts = 0
    server.use(
      http.post('/api/things', () => {
        attempts += 1
        return apiError(400, ErrorCode.InvalidCsrfToken)
      }),
    )

    const error = await failure(httpClient('/api/things', { method: 'POST' }))

    expect(attempts).toBe(2)
    expect(error.code).toBe(ErrorCode.InvalidCsrfToken)
  })

  it('does not retry safe requests that fail with InvalidCsrfToken', async () => {
    let attempts = 0
    server.use(
      http.get('/api/things', () => {
        attempts += 1
        return apiError(400, ErrorCode.InvalidCsrfToken)
      }),
    )

    const error = await failure(httpClient('/api/things'))

    expect(attempts).toBe(1)
    expect(error.status).toBe(400)
  })

  it('does not retry unsafe requests failing with another code', async () => {
    let attempts = 0
    server.use(
      http.post('/api/things', () => {
        attempts += 1
        return apiError(409, ErrorCode.EventNotFound)
      }),
    )

    const error = await failure(httpClient('/api/things', { method: 'POST' }))

    expect(attempts).toBe(1)
    expect(error.code).toBe(ErrorCode.EventNotFound)
  })

  describe('error parsing', () => {
    it('builds an ApiError from a problem-details body preferring detail', async () => {
      server.use(
        http.get('/api/things', () =>
          apiError(404, ErrorCode.EventNotFound, { detail: 'Detailed reason' }),
        ),
      )

      const error = await failure(httpClient('/api/things'))

      expect(error).toBeInstanceOf(Error)
      expect(error.name).toBe('ApiError')
      expect(error.status).toBe(404)
      expect(error.message).toBe('Detailed reason')
      expect(error.traceId).toBe('trace-123')
      expect(error.code).toBe(ErrorCode.EventNotFound)
    })

    it('falls back to the title', async () => {
      server.use(http.get('/api/titled', () => apiError(422)))

      const titled = await failure(httpClient('/api/titled'))

      expect(titled.message).toBe('Error 422')
      expect(titled.code).toBeUndefined()
    })

    it('uses a generic message for JSON errors without text fields', async () => {
      server.use(http.get('/api/things', () => HttpResponse.json({}, { status: 503 })))

      const error = await failure(httpClient('/api/things'))

      expect(error.message).toBe('Error 503')
    })

    it('uses a plain-text body as the message', async () => {
      server.use(
        http.get('/api/things', () => new HttpResponse('Gateway exploded', { status: 502 })),
      )

      const error = await failure(httpClient('/api/things'))

      expect(error.message).toBe('Gateway exploded')
      expect(error.code).toBeUndefined()
    })

    it('keeps the generic message for an empty non-JSON body', async () => {
      server.use(http.get('/api/things', () => new HttpResponse(null, { status: 500 })))

      const error = await failure(httpClient('/api/things'))

      expect(error.message).toBe('Error 500')
    })

    it('keeps the generic message when a JSON error body is malformed', async () => {
      server.use(
        http.get(
          '/api/things',
          () =>
            new HttpResponse('{not json', {
              status: 500,
              headers: { 'Content-Type': 'application/json' },
            }),
        ),
      )

      const error = await failure(httpClient('/api/things'))

      expect(error.message).toBe('Error 500')
      expect(error.traceId).toBeUndefined()
    })
  })

  describe('response parsing', () => {
    it('returns undefined data for an empty JSON body', async () => {
      server.use(
        http.get(
          '/api/things',
          () => new HttpResponse('', { headers: { 'Content-Type': 'application/json' } }),
        ),
      )

      const result = await httpClient<ClientResult>('/api/things')

      expect(result.data).toBeUndefined()
    })

    it('returns text for text responses and undefined when the text is empty', async () => {
      server.use(
        http.get('/api/text', () => HttpResponse.text('hello')),
        http.get('/api/empty-text', () => HttpResponse.text('')),
      )

      const text = await httpClient<ClientResult>('/api/text')
      const empty = await httpClient<ClientResult>('/api/empty-text')

      expect(text.data).toBe('hello')
      expect(empty.data).toBeUndefined()
    })

    it('returns a Blob for binary responses and undefined when it is empty', async () => {
      server.use(
        http.get(
          '/api/file',
          () =>
            new HttpResponse(new Uint8Array([1, 2, 3]), {
              headers: { 'Content-Type': 'application/pdf' },
            }),
        ),
        http.get('/api/no-body', () => new HttpResponse(null, { status: 200 })),
      )

      const file = await httpClient<ClientResult>('/api/file')
      const empty = await httpClient<ClientResult>('/api/no-body')

      expect(file.data).toBeInstanceOf(Blob)
      expect((file.data as Blob).size).toBe(3)
      expect(empty.data).toBeUndefined()
    })
  })
})

describe('ApiError', () => {
  it('stores status, trace id and code', () => {
    const error = new ApiError(418, 'Teapot', 'trace-9', ErrorCode.EventNotFound)

    expect(error.message).toBe('Teapot')
    expect(error.status).toBe(418)
    expect(error.traceId).toBe('trace-9')
    expect(error.code).toBe(ErrorCode.EventNotFound)
  })
})
