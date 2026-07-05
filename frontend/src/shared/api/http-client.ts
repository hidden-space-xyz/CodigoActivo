import { env } from '@/shared/config'
import { ErrorCode } from '@/shared/api/generated/models'

const UNSAFE_METHODS: ReadonlySet<string> = new Set(['POST', 'PUT', 'PATCH', 'DELETE'])

export class ApiError extends Error {
  readonly status: number
  readonly traceId?: string | undefined
  readonly code?: ErrorCode | undefined

  constructor(
    status: number,
    message: string,
    traceId?: string | undefined,
    code?: ErrorCode | undefined,
  ) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.traceId = traceId
    this.code = code
  }
}

let csrfToken: string | null = null
let csrfHeaderName = 'X-CSRF-TOKEN'
let csrfTokenPromise: Promise<void> | null = null

export function resetCsrfToken(): void {
  csrfToken = null
}

async function fetchCsrfToken(): Promise<void> {
  const { getApiAuthCsrf } = await import('@/shared/api/generated/endpoints/auth/auth')
  try {
    const response = await getApiAuthCsrf()
    csrfToken = response.data.token ?? null
    if (response.data.headerName) csrfHeaderName = response.data.headerName
  } catch {
    csrfToken = null
  }
}

async function ensureCsrfToken(): Promise<void> {
  if (csrfToken) return
  csrfTokenPromise ??= fetchCsrfToken().finally(() => {
    csrfTokenPromise = null
  })
  await csrfTokenPromise
}

async function buildApiError(response: Response): Promise<ApiError> {
  let message = `Error ${response.status}`
  let traceId: string | undefined
  let code: ErrorCode | undefined
  try {
    const contentType = response.headers.get('content-type') ?? ''
    if (contentType.includes('json')) {
      const detail = (await response.json()) as {
        title?: string
        detail?: string
        message?: string
        traceId?: string
        code?: ErrorCode
      }
      message = detail.detail ?? detail.title ?? detail.message ?? message
      traceId = detail.traceId
      code = detail.code
    } else {
      const text = await response.text()
      if (text) message = text
    }
  } catch {}
  return new ApiError(response.status, message, traceId, code)
}

async function parseData(response: Response): Promise<unknown> {
  if (response.status === 204) return undefined
  const contentType = response.headers.get('content-type') ?? ''
  if (contentType.includes('json')) {
    const text = await response.text()
    return text ? JSON.parse(text) : undefined
  }
  if (contentType.startsWith('text/')) {
    const text = await response.text()
    return text || undefined
  }
  const blob = await response.blob()
  return blob.size > 0 ? blob : undefined
}

async function request<T>(url: string, init: RequestInit, retry: boolean): Promise<T> {
  const method = (init.method ?? 'GET').toUpperCase()
  const headers = new Headers(init.headers)
  if (!headers.has('Accept')) headers.set('Accept', 'application/json')

  if (UNSAFE_METHODS.has(method)) {
    await ensureCsrfToken()
    if (csrfToken) headers.set(csrfHeaderName, csrfToken)
  }

  const response = await fetch(`${env.apiBaseUrl}${url}`, {
    ...init,
    headers,
    credentials: 'include',
  })

  if (!response.ok) {
    const error = await buildApiError(response)
    if (UNSAFE_METHODS.has(method) && retry && error.code === ErrorCode.InvalidCsrfToken) {
      resetCsrfToken()
      return request<T>(url, init, false)
    }
    throw error
  }

  const data = await parseData(response)
  return { status: response.status, data, headers: response.headers } as T
}

export const httpClient = <T>(url: string, init: RequestInit = {}): Promise<T> => {
  return request<T>(url, init, true)
}
