import { request, type APIRequestContext, type APIResponse } from '@playwright/test'
import type { ApiErrorResponse } from '@contract/apiErrorResponse'
import type { CsrfTokenResponse } from '@contract/csrfTokenResponse'
import type { ErrorCode } from '@contract/errorCode'

import { environment } from '../config/environment'

/** Cookies and origins of a signed-in session, as Playwright stores them. */
export type StorageState = Awaited<ReturnType<APIRequestContext['storageState']>>

/** Query string values; `undefined` entries are left out. */
export type Query = Readonly<Record<string, string | number | boolean | undefined>>

/** A file sent as the `file` part of a multipart upload. */
export interface UploadFile {
  readonly name: string
  readonly mimeType: string
  readonly buffer: Buffer
}

/** How an `ApiClient` connects: the client address it reports and the session it starts from. */
export interface ApiClientOptions {
  readonly clientAddress: string
  readonly storageState?: StorageState
}

type Method = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'

interface SendOptions {
  readonly query?: Query | undefined
  readonly data?: unknown
  readonly multipart?: Readonly<Record<string, UploadFile>>
}

/** A request the API answered with an error status, carrying its `ApiErrorResponse` code. */
export class ApiRequestError extends Error {
  /** HTTP status of the answer. */
  readonly status: number

  /** Wire error code of the answer, when it had one. */
  readonly code: ErrorCode | undefined

  /** Trace of the request in the API logs, when the answer carried one. */
  readonly traceId: string | undefined

  constructor(method: Method, path: string, status: number, body: ApiErrorResponse | undefined) {
    super(`${method} ${path} answered ${status} ${body?.code ?? ''} ${body?.title ?? ''}`.trim())
    this.name = 'ApiRequestError'
    this.status = status
    this.code = body?.code
    this.traceId = body?.traceId
  }
}

/**
 * Talks to the API through nginx the way the SPA does: same origin, session cookies, the CSRF
 * header on unsafe methods (fetched on demand and refreshed once when the API refuses it) and the
 * caller's address in `X-Forwarded-For`, so the rate limits count each worker as its own client.
 */
export class ApiClient {
  private csrfToken: string | undefined

  private constructor(private readonly http: APIRequestContext) {}

  /** Opens a client, signed in when `storageState` holds a session. */
  static async open(options: ApiClientOptions): Promise<ApiClient> {
    const http = await request.newContext({
      baseURL: environment.baseUrl,
      extraHTTPHeaders: { 'X-Forwarded-For': options.clientAddress },
      ...(options.storageState ? { storageState: options.storageState } : {}),
    })
    return new ApiClient(http)
  }

  /** Sends a GET and returns the parsed body. */
  get<T>(path: string, query?: Query): Promise<T> {
    return this.send<T>('GET', path, { query })
  }

  /** Sends a JSON POST and returns the parsed body, `undefined` for 204. */
  post<T>(path: string, data?: unknown, query?: Query): Promise<T> {
    return this.send<T>('POST', path, { data, query })
  }

  /** Sends a JSON PUT and returns the parsed body. */
  put<T>(path: string, data?: unknown): Promise<T> {
    return this.send<T>('PUT', path, { data })
  }

  /** Sends a JSON PATCH and returns the parsed body, `undefined` for 204. */
  patch<T>(path: string, data?: unknown, query?: Query): Promise<T> {
    return this.send<T>('PATCH', path, { data, query })
  }

  /** Sends a DELETE and returns the parsed body, if any. */
  delete<T>(path: string): Promise<T> {
    return this.send<T>('DELETE', path, {})
  }

  /** Uploads `file` as the multipart `file` part and returns the parsed body. */
  upload<T>(path: string, file: UploadFile): Promise<T> {
    return this.send<T>('POST', path, { multipart: { file } })
  }

  /** Drops the CSRF token, which the API binds to the signed-in identity. */
  forgetCsrfToken(): void {
    this.csrfToken = undefined
  }

  /** The cookies of the current session, to open a browser context signed in as the same user. */
  storageState(): Promise<StorageState> {
    return this.http.storageState()
  }

  /** Releases the underlying request context. */
  dispose(): Promise<void> {
    return this.http.dispose()
  }

  private async send<T>(method: Method, path: string, options: SendOptions): Promise<T> {
    let response = await this.fetch(method, path, options)
    if (method !== 'GET' && (await errorCodeOf(response)) === 'InvalidCsrfToken') {
      this.forgetCsrfToken()
      response = await this.fetch(method, path, options)
    }
    if (!response.ok()) {
      throw new ApiRequestError(method, path, response.status(), await errorBodyOf(response))
    }
    return (await bodyOf(response)) as T
  }

  private async fetch(method: Method, path: string, options: SendOptions): Promise<APIResponse> {
    const headers = method === 'GET' ? {} : { 'X-CSRF-TOKEN': await this.csrf() }
    return this.http.fetch(path, {
      method,
      headers,
      params: definedEntries(options.query),
      ...(options.multipart ? { multipart: options.multipart } : {}),
      ...(options.data === undefined ? {} : { data: options.data }),
    })
  }

  private async csrf(): Promise<string> {
    if (this.csrfToken) return this.csrfToken
    const response = await this.http.get('/api/auth/csrf')
    if (!response.ok()) {
      throw new ApiRequestError('GET', '/api/auth/csrf', response.status(), undefined)
    }
    const { token } = (await response.json()) as CsrfTokenResponse
    this.csrfToken = token
    return token
  }
}

function definedEntries(query: Query | undefined): Record<string, string | number | boolean> {
  const entries: [string, string | number | boolean][] = []
  for (const [key, value] of Object.entries(query ?? {})) {
    if (value !== undefined) entries.push([key, value])
  }
  return Object.fromEntries(entries)
}

async function bodyOf(response: APIResponse): Promise<unknown> {
  const text = await response.text()
  if (!text) return undefined
  const contentType = response.headers()['content-type'] ?? ''
  return contentType.includes('json') ? JSON.parse(text) : text
}

async function errorBodyOf(response: APIResponse): Promise<ApiErrorResponse | undefined> {
  const body = await bodyOf(response)
  return typeof body === 'object' && body !== null ? (body as ApiErrorResponse) : undefined
}

async function errorCodeOf(response: APIResponse): Promise<ErrorCode | undefined> {
  return response.ok() ? undefined : (await errorBodyOf(response))?.code
}
