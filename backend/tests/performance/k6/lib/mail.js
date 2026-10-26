import http from 'k6/http'
import { fail, sleep } from 'k6'
import { MAILPIT_URL } from './config.js'

export const LOGIN_CODE = /^\s*(\d{6})\s*$/m
export const VERIFICATION_LINK = /\/verify-account#userId=([0-9a-f-]{36})&code=([^\s<>"')\]]+)/
export const RESET_LINK = /\/reset-password#userId=([0-9a-f-]{36})&code=([^\s<>"')\]]+)/
export const EMAIL_CHANGE_LINK = /\/confirm-email#userId=([0-9a-f-]{36})&code=([^\s<>"')\]]+)/
export const UNSUBSCRIBE_LINK = /\/unsubscribe#token=([^\s<>"')\]]+)/

const params = { tags: { name: 'mailpit', target: 'mailpit' } }

function messagesTo(address) {
  const search = encodeURIComponent(`to:"${address}"`)
  const res = http.get(`${MAILPIT_URL}/api/v1/search?query=${search}&limit=50`, params)
  return res.status === 200 ? res.json('messages') || [] : []
}

function textOf(id, cache) {
  if (!(id in cache)) {
    const res = http.get(`${MAILPIT_URL}/api/v1/message/${id}`, params)
    cache[id] = res.status === 200 ? res.json('Text') || '' : ''
  }
  return cache[id]
}

function find({ address, pattern, since }, cache, includeOlder) {
  for (const message of messagesTo(address)) {
    if (includeOlder || Date.parse(message.Created) >= since - 1000) {
      const match = textOf(message.ID, cache).match(pattern)
      if (match) {
        return match.slice(1).map(decodeURIComponent)
      }
    }
  }
  return null
}

export function collectMail(requests, { timeout = 60, reuse = false } = {}) {
  const results = new Array(requests.length).fill(null)
  const cache = {}
  const deadline = Date.now() + timeout * 1000
  let pending = requests.map((_, index) => index)
  while (pending.length) {
    pending = pending.filter((index) => !(results[index] = find(requests[index], cache, false)))
    if (!pending.length || Date.now() >= deadline) {
      break
    }
    sleep(0.5)
  }
  if (reuse) {
    pending = pending.filter((index) => !(results[index] = find(requests[index], cache, true)))
  }
  if (pending.length) {
    const missing = pending.map((index) => requests[index].address).join(', ')
    fail(`No expected email reached ${missing} within ${timeout}s`)
  }
  return results
}

export function waitForMail(address, pattern, { since, timeout = 60, reuse = false } = {}) {
  return collectMail([{ address, pattern, since }], { timeout, reuse })[0]
}
