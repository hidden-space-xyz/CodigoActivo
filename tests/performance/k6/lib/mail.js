import http from 'k6/http'
import { fail, sleep } from 'k6'
import { Trend } from 'k6/metrics'
import { MAILPIT_URL } from './config.js'

export const LOGIN_CODE = /^\s*(\d{6})\s*$/m
export const VERIFICATION_LINK = /\/verify-account#userId=([0-9a-f-]{36})&code=([^\s<>"')\]]+)/
export const RESET_LINK = /\/reset-password#userId=([0-9a-f-]{36})&code=([^\s<>"')\]]+)/

const TIMEOUT_SECONDS = 60
const CREATED_TOLERANCE_MS = 1000

const mailDelay = new Trend('mail_delay', true)
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

function find({ address, pattern, since }, cache) {
  for (const message of messagesTo(address)) {
    if (Date.parse(message.Created) >= since - CREATED_TOLERANCE_MS) {
      const match = textOf(message.ID, cache).match(pattern)
      if (match) {
        return match.slice(1).map(decodeURIComponent)
      }
    }
  }
  return null
}

/**
 * Polls Mailpit until each request `{ address, pattern, since }` finds a message newer than `since`
 * and returns the groups of each pattern, in the order of the requests. Fails when one never comes.
 */
export function collectMail(requests) {
  const results = new Array(requests.length).fill(null)
  const cache = {}
  const deadline = Date.now() + TIMEOUT_SECONDS * 1000
  let pending = requests.map((_, index) => index)
  while (pending.length) {
    pending = pending.filter((index) => !(results[index] = find(requests[index], cache)))
    if (pending.length && Date.now() >= deadline) {
      const missing = pending.map((index) => requests[index].address).join(', ')
      fail(`No expected email reached ${missing} within ${TIMEOUT_SECONDS}s`)
    }
    if (pending.length) {
      sleep(0.5)
    }
  }
  return results
}

/** Waits for one message during a scenario and records how long its reader waited for it. */
export function waitForMail(address, pattern, since) {
  try {
    return collectMail([{ address, pattern, since }])[0]
  } finally {
    mailDelay.add(Date.now() - since)
  }
}
