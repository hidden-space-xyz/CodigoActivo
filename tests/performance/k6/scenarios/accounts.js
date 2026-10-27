import { sleep } from 'k6'
import exec from 'k6/execution'
import { Secret, TOTP } from 'https://cdn.jsdelivr.net/npm/otpauth@9.5.2/dist/otpauth.esm.min.js'
import { NEW_PASSWORD, PASSWORD } from '../lib/config.js'
import { body, clientIp, query, Session, think } from '../lib/http.js'
import { sessionFor } from '../lib/pool.js'
import { waitForCooldown } from '../lib/accounts.js'
import {
  EMAIL_CHANGE_LINK,
  LOGIN_CODE,
  RESET_LINK,
  UNSUBSCRIBE_LINK,
  VERIFICATION_LINK,
  waitForMail,
} from '../lib/mail.js'
import { CATALOG, email, minor, registration } from '../lib/data.js'
import { message } from './mailing.js'

const STEP_MS = 30000

function authenticatorCode(secret, lastStep) {
  const wait = (lastStep + 1) * STEP_MS - Date.now()
  if (wait > 0) {
    sleep(wait / 1000 + 0.5)
  }
  const totp = new TOTP({
    secret: Secret.fromBase32(secret.replace(/\s/g, '')),
    digits: 6,
    period: 30,
  })
  return { code: totp.generate(), step: Math.floor(Date.now() / STEP_MS) }
}

function startLogin(session, account, password) {
  session.csrf = null
  return body(
    session.post('/api/auth/login', {
      name: 'POST /api/auth/login',
      json: { identifier: account.email, password, keepSignedIn: false },
    }),
  )
}

function finishLogin(session, code) {
  session.user = body(
    session.post('/api/auth/login/two-factor', {
      name: 'POST /api/auth/login/two-factor',
      json: { code },
    }),
  )
  session.refreshCsrf()
}

function emailLogin(session, account, password, resend = false) {
  waitForCooldown(account)
  const since = Date.now()
  startLogin(session, account, password)
  session.get('/api/auth/login/two-factor', { name: 'GET /api/auth/login/two-factor' })
  if (resend) {
    session.post('/api/auth/login/two-factor/resend', {
      name: 'POST /api/auth/login/two-factor/resend',
      expect: [409],
    })
  }
  const [code] = waitForMail(account.email, LOGIN_CODE, { since })
  account.codeAt = since
  finishLogin(session, code)
}

function profile(user, changes = {}) {
  return {
    firstName: user.firstName,
    lastName: user.lastName,
    email: user.email,
    phone: user.phone,
    nationalId: user.nationalId,
    newsConsent: user.newsConsent,
    gender: user.gender,
    ...changes,
  }
}

export function accountLifecycle(data) {
  const iteration = exec.scenario.iterationInTest
  const tag = `${data.runId}-${iteration}`
  const account = { email: email('cuenta', tag), codeAt: 0 }
  const session = new Session({ ip: clientIp('lifecycle', iteration * 2), actor: 'member' })
  const visitor = new Session({ ip: clientIp('lifecycle', iteration * 2 + 1) })
  const admin = sessionFor(data.admins.mailing[0])

  let since = Date.now()
  session.post('/api/auth/register', {
    name: 'POST /api/auth/register',
    json: registration(account.email, 1),
    expect: [204],
  })
  session.post('/api/auth/resend-verification', {
    name: 'POST /api/auth/resend-verification',
    json: { email: account.email },
    expect: [204],
  })
  const [userId, verification] = waitForMail(account.email, VERIFICATION_LINK, { since })
  session.patch(`/api/auth/${userId}/verify`, {
    name: 'PATCH /api/auth/{userId}/verify',
    json: { otp: verification },
  })
  think()

  emailLogin(session, account, PASSWORD, true)
  const user = body(session.get('/api/auth/me', { name: 'GET /api/auth/me' }))
  session.put(`/api/users/${userId}`, {
    name: 'PUT /api/users/{userId}',
    json: profile(user, { firstName: `${user.firstName} María` }),
  })
  const child = body(
    session.post(`/api/users/${userId}/children`, {
      name: 'POST /api/users/{userId}/children',
      json: minor(),
      expect: [201],
    }),
  )
  think()

  since = Date.now()
  admin.post(`/api/emails/users/${userId}`, {
    name: 'POST /api/emails/users/{userId}',
    form: message('News'),
  })
  const [token] = waitForMail(account.email, UNSUBSCRIBE_LINK, { since })
  visitor.post('/api/emails/unsubscribe', {
    name: 'POST /api/emails/unsubscribe',
    json: { token },
    expect: [204],
  })
  visitor.post(`/api/emails/unsubscribe/one-click${query({ token })}`, {
    name: 'POST /api/emails/unsubscribe/one-click',
    form: { 'List-Unsubscribe': 'One-Click' },
    csrf: false,
    expect: [204],
  })
  admin.patch(
    `/api/users/${userId}/change-type${query({ userTypeId: CATALOG.userTypes.member })}`,
    {
      name: 'PATCH /api/users/{userId}/change-type',
    },
  )
  admin.patch(`/api/users/${userId}/admin`, {
    name: 'PATCH /api/users/{userId}/admin',
    json: { isAdmin: true, currentPassword: PASSWORD },
    expect: [204],
  })
  admin.patch(`/api/users/${userId}/admin`, {
    name: 'PATCH /api/users/{userId}/admin',
    json: { isAdmin: false },
    expect: [204],
  })
  think()

  const setup = body(
    session.post('/api/auth/two-factor/authenticator/setup', {
      name: 'POST /api/auth/two-factor/authenticator/setup',
      json: { currentPassword: PASSWORD },
    }),
  )
  let totp = authenticatorCode(setup.sharedKey, 0)
  session.post('/api/auth/two-factor/authenticator/confirm', {
    name: 'POST /api/auth/two-factor/authenticator/confirm',
    json: { code: totp.code },
    expect: [204],
  })
  session.post('/api/auth/logout', { name: 'POST /api/auth/logout', expect: [204] })
  startLogin(session, account, PASSWORD)
  totp = authenticatorCode(setup.sharedKey, totp.step)
  finishLogin(session, totp.code)
  think()

  if (iteration % 2) {
    admin.post(`/api/users/${userId}/two-factor/reset`, {
      name: 'POST /api/users/{userId}/two-factor/reset',
      json: { currentPassword: PASSWORD },
      expect: [204],
    })
  } else {
    totp = authenticatorCode(setup.sharedKey, totp.step)
    session.post('/api/auth/two-factor/email', {
      name: 'POST /api/auth/two-factor/email',
      json: { currentPassword: PASSWORD, code: totp.code },
      expect: [204],
    })
  }
  session.patch(`/api/users/${userId}/password`, {
    name: 'PATCH /api/users/{userId}/password',
    json: { currentPassword: PASSWORD, newPassword: NEW_PASSWORD },
    expect: [204],
  })
  emailLogin(session, account, NEW_PASSWORD)
  think()

  const changed = email('cuenta', tag, 'nueva')
  since = Date.now()
  session.put(`/api/users/${userId}`, {
    name: 'PUT /api/users/{userId}',
    json: profile(session.user, { email: changed, currentPassword: NEW_PASSWORD }),
  })
  const [, change] = waitForMail(changed, EMAIL_CHANGE_LINK, { since })
  session.patch(`/api/auth/${userId}/confirm-email`, {
    name: 'PATCH /api/auth/{userId}/confirm-email',
    json: { otp: change },
    expect: [204],
  })
  account.email = changed
  think()

  since = Date.now()
  visitor.post('/api/auth/forgot-password', {
    name: 'POST /api/auth/forgot-password',
    json: { email: changed },
    expect: [204],
  })
  const [, reset] = waitForMail(changed, RESET_LINK, { since })
  visitor.patch(`/api/auth/${userId}/reset-password`, {
    name: 'PATCH /api/auth/{userId}/reset-password',
    json: { otp: reset, newPassword: PASSWORD },
    expect: [204],
  })
  emailLogin(session, account, PASSWORD)
  think()

  admin.delete(`/api/users/${child.id}`, { name: 'DELETE /api/users/{userId}', expect: [204] })
  session.get('/api/me/deletion', { name: 'GET /api/me/deletion' })
  waitForCooldown(account)
  since = Date.now()
  session.post('/api/me/deletion/code', {
    name: 'POST /api/me/deletion/code',
    json: { currentPassword: PASSWORD },
    expect: [204],
  })
  const [code] = waitForMail(account.email, LOGIN_CODE, { since })
  session.post('/api/me/deletion', {
    name: 'POST /api/me/deletion',
    json: { currentPassword: PASSWORD, code },
    expect: [204],
  })
}
