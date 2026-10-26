import http from 'k6/http'
import { check, sleep } from 'k6'
import { Counter } from 'k6/metrics'
import { API_URL, PROFILE } from './config.js'
import { KINDS } from './catalog.js'
import { currentUsers } from './steps.js'

const unsafeMethods = new Set(['POST', 'PUT', 'PATCH', 'DELETE'])
const rateLimited = new Counter('rate_limited')
const unexpected = new Counter('unexpected_responses')
const warnings = {}
const ipRanges = { visitor: 1, account: 2, setup: 3, lifecycle: 4 }

const BATCH_SIZE = 16

export function clientIp(kind, index) {
  const value = index % 65536
  return `10.${ipRanges[kind]}.${value >> 8}.${value & 255}`
}

export function think(scale = 1) {
  const [min, max] = PROFILE.think
  if (max > 0) {
    sleep((min + Math.random() * (max - min)) * scale)
  }
}

export function query(params = {}) {
  const pairs = Object.entries(params)
    .filter(([, value]) => value !== undefined && value !== null && value !== '')
    .map(([key, value]) => `${encodeURIComponent(key)}=${encodeURIComponent(value)}`)
  return pairs.length ? `?${pairs.join('&')}` : ''
}

export function body(res) {
  try {
    return res.json()
  } catch {
    return null
  }
}

export function parallel(calls) {
  const responses = []
  for (let start = 0; start < calls.length; start += BATCH_SIZE) {
    const chunk = calls.slice(start, start + BATCH_SIZE)
    const prepared = chunk.map(({ session, method, path, options }) =>
      session.prepare(method, path, options),
    )
    const results = http.batch(prepared.map((item) => item.request))
    results.forEach((res, index) => responses.push(prepared[index].settle(res)))
  }
  return responses
}

function expiresAt(cookie) {
  return typeof cookie.expires === 'string' ? Date.parse(cookie.expires) : cookie.expires || 0
}

function tagsFor(name, actor) {
  const tags = { name, actor, target: 'api', kind: KINDS[name] }
  const users = currentUsers()
  if (users) {
    tags.users = users
  }
  return tags
}

function warn(name, res) {
  warnings[name] = (warnings[name] || 0) + 1
  if (warnings[name] <= 3) {
    const detail = typeof res.body === 'string' ? res.body.slice(0, 300) : ''
    console.warn(`${name} answered ${res.status} ${res.error || ''} ${detail}`)
  }
}

export class Session {
  constructor({ ip, actor = 'anonymous', cookies = {}, csrf = null, user = null } = {}) {
    this.ip = ip
    this.actor = actor
    this.cookies = { ...cookies }
    this.csrf = csrf
    this.user = user
  }

  static restore(data) {
    return new Session(data)
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

  prepare(method, path, options = {}) {
    const { json, form, name, expect = [200], headers = {}, responseType = 'text' } = options
    const requestHeaders = { 'X-Forwarded-For': this.ip, 'X-Forwarded-Proto': 'https', ...headers }
    let payload = form ?? null
    if (json !== undefined) {
      payload = JSON.stringify(json)
      requestHeaders['Content-Type'] = 'application/json'
    }
    if (unsafeMethods.has(method) && options.csrf !== false) {
      requestHeaders['X-CSRF-TOKEN'] = this.ensureCsrf()
    }
    const tags = tagsFor(name, this.actor)
    return {
      request: {
        method,
        url: `${API_URL}${path}`,
        body: payload,
        params: {
          headers: requestHeaders,
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
    const expected = expect.includes(res.status)
    check(res, { 'expected status': () => expected }, tags)
    if (res.status === 429) {
      rateLimited.add(1, tags)
    }
    if (!expected) {
      unexpected.add(1, tags)
      warn(tags.name, res)
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

  ensureCsrf() {
    return this.csrf || this.refreshCsrf()
  }

  refreshCsrf() {
    const res = this.get('/api/auth/csrf', { name: 'GET /api/auth/csrf' })
    this.csrf = body(res)?.token ?? null
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

  forget() {
    this.cookies = {}
    this.csrf = null
    this.user = null
  }
}
