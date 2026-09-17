import { http, HttpResponse, type HttpHandler } from 'msw'
import { setupServer } from 'msw/node'

import type { ErrorCode } from '@/shared/api/generated/models'

/** CSRF token served by the default handler and expected on unsafe requests. */
export const TEST_CSRF_TOKEN = 'test-csrf-token'

/**
 * Handlers every test starts with: a CSRF token and an anonymous session. Tests add their own
 * handlers with `server.use(...)`; they are removed after each test.
 */
const defaultHandlers: HttpHandler[] = [
  http.get('/api/auth/csrf', () =>
    HttpResponse.json({ token: TEST_CSRF_TOKEN, headerName: 'X-CSRF-TOKEN' }),
  ),
  http.get('/api/auth/me', () => new HttpResponse(null, { status: 401 })),
]

/** In-process mock API. It replaces the backend for every unit and integration test. */
export const server = setupServer(...defaultHandlers)

/** Problem-details error body in the shape the API returns. */
export function apiError(
  status: number,
  code?: ErrorCode,
  extra: Record<string, unknown> = {},
): HttpResponse<Record<string, unknown>> {
  return HttpResponse.json(
    { title: `Error ${status}`, ...(code ? { code } : {}), traceId: 'trace-123', ...extra },
    { status },
  )
}

/** Paged-result body in the shape the API returns. */
export function paged<T>(items: T[], total = items.length): { items: T[]; total: number } {
  return { items, total }
}

export { http, HttpResponse }
