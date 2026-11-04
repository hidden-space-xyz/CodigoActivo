import http from 'k6/http'
import { Counter } from 'k6/metrics'
import { API_URL } from './config.js'
import { KIND_OF } from './catalog.js'
import { currentWindow, inRun } from './windows.js'

const unsafeMethods = new Set(['POST', 'PUT', 'PATCH', 'DELETE'])
const ipRanges = { visitor: 1, account: 2, setup: 3, newcomer: 4 }
const rateLimited = new Counter('rate_limited')
const reported = {}

export function clientIp(kind, index) {
  const value = index % 65536
  return `10.${ipRanges[kind]}.${value >> 8}.${value & 255}`
}

export function body(res) {
  try {
    return res.json()
  } catch {
    return null
  }
}

export function parallel(calls) {
  const prepared = calls.map(({ session, method, path, options }) =>
    session.prepare(method, path, options),
  )
  return http
    .batch(prepared.map(({ request }) => request))
    .map((res, index) => prepared[index].settle(res))
}

function url(path, route = {}, query = {}) {
  const resolved = path.replace(/\{(\w+)\}/g, (_, key) => {
    if (route[key] === undefined) {
      throw new Error(`${path} needs a value for {${key}}`)
    }
    return encodeURIComponent(route[key])
  })
  const pairs = Object.entries(query)
    .filter(([, value]) => value !== undefined && value !== null && value !== '')
    .map(([key, value]) => `${encodeURIComponent(key)}=${encodeURIComponent(value)}`)
  return `${API_URL}${resolved}${pairs.length ? `?${pairs.join('&')}` : ''}`
}

function tagsFor(name, actor) {
  const tags = { name, actor, target: 'api' }
  const kind = KIND_OF[name]
  if (kind) {
    tags.kind = kind
  } else if (inRun()) {
    reportOnce(`uncatalogued ${name}`, `${name} has no budget in lib/catalog.js`)
  }
  const window = currentWindow()
  if (window) {
    tags.window = window
  }
  return tags
}

function reportOnce(key, message) {
  reported[key] = (reported[key] || 0) + 1
  if (reported[key] <= 3) {
    console.warn(message)
  }
}

function expiresAt(cookie) {
  return typeof cookie.expires === 'string' ? Date.parse(cookie.expires) : cookie.expires || 0
}

export class Session {
  constructor({ ip, actor = 'anonymous', cookies = {}, csrf = null, user = null } = {}) {
    this.ip = ip
    this.actor = actor
    this.cookies = { ...cookies }
    this.csrf = csrf
    this.user = user
  }

  snapshot() {
    return {
      ip: this.ip,
      actor: this.actor,
      cookies: { ...this.cookies },
      csrf: this.csrf,
      user: this.user,
    }
  }

  get userId() {
    return this.user?.id
  }

  name(method, path) {
    return method === 'GET' && !this.user ? `${method} ${path} [anonymous]` : `${method} ${path}`
  }

  prepare(method, path, options = {}) {
    const { route, query, json, form, expect = [200], responseType = 'text' } = options
    const headers = { 'X-Forwarded-For': this.ip, 'X-Forwarded-Proto': 'https' }
    let payload = form ?? null
    if (json !== undefined) {
      payload = JSON.stringify(json)
      headers['Content-Type'] = 'application/json'
    }
    if (unsafeMethods.has(method) && options.csrf !== false) {
      headers['X-CSRF-TOKEN'] = this.csrf || this.refreshCsrf()
    }
    const tags = tagsFor(this.name(method, path), this.actor)
    return {
      request: {
        method,
        url: url(path, route, query),
        body: payload,
        params: {
          headers,
          cookies: { ...this.cookies },
          tags,
          responseCallback: http.expectedStatuses(...expect),
          responseType,
          redirects: 0,
        },
      },
      settle: (res) => this.settle(res, tags, expect),
    }
  }

  settle(res, tags, expect) {
    this.keepCookies(res)
    if (res.status === 429) {
      rateLimited.add(1, tags)
    }
    if (!expect.includes(res.status)) {
      const detail = typeof res.body === 'string' ? res.body.slice(0, 300) : ''
      reportOnce(tags.name, `${tags.name} answered ${res.status} ${res.error || ''} ${detail}`)
    }
    return res
  }

  request(method, path, options) {
    const { request, settle } = this.prepare(method, path, options)
    return settle(http.request(request.method, request.url, request.body, request.params))
  }

  get(path, options) {
    return this.request('GET', path, options)
  }

  post(path, options) {
    return this.request('POST', path, options)
  }

  put(path, options) {
    return this.request('PUT', path, options)
  }

  patch(path, options) {
    return this.request('PATCH', path, options)
  }

  delete(path, options) {
    return this.request('DELETE', path, options)
  }

  refreshCsrf() {
    this.csrf = body(this.get('/api/auth/csrf'))?.token ?? null
    return this.csrf
  }

  keepCookies(res) {
    const now = Date.now()
    for (const [name, values] of Object.entries(res.cookies)) {
      const cookie = values[values.length - 1]
      const expires = expiresAt(cookie)
      if (!cookie.value || cookie.max_age < 0 || (expires > 0 && expires <= now)) {
        delete this.cookies[name]
      } else {
        this.cookies[name] = cookie.value
      }
    }
  }
}
