import { fail, sleep } from 'k6'
import { ADMIN_EMAIL, DEMO_EMAIL_DOMAIN, PASSWORD } from './config.js'
import { body, clientIp, parallel, query, Session } from './http.js'
import { collectMail, LOGIN_CODE, RESET_LINK, VERIFICATION_LINK } from './mail.js'
import { registration } from './data.js'

const CODE_COOLDOWN_MS = 61000

function expectAll(responses, statuses, what) {
  const failed = responses.filter((res) => !statuses.includes(res.status))
  if (failed.length) {
    fail(`${what} failed for ${failed.length} account(s): ${failed[0].status} ${failed[0].body}`)
  }
  return responses.map(body)
}

export function waitForCooldown(account) {
  const wait = (account.codeAt || 0) + CODE_COOLDOWN_MS - Date.now()
  if (wait > 0) {
    sleep(wait / 1000)
  }
}

function signIn(accounts, { attempt = false } = {}) {
  parallel(
    accounts.map(({ session }) => ({
      session,
      method: 'GET',
      path: '/api/auth/csrf',
      options: { name: 'GET /api/auth/csrf' },
    })),
  ).forEach((res, index) => (accounts[index].session.csrf = body(res)?.token ?? null))
  const since = Date.now()
  const challenges = parallel(
    accounts.map(({ session, email, password = PASSWORD }) => ({
      session,
      method: 'POST',
      path: '/api/auth/login',
      options: {
        name: 'POST /api/auth/login',
        json: { identifier: email, password, keepSignedIn: true },
        expect: attempt ? [200, 401] : [200],
      },
    })),
  )
  const open = accounts.filter((_, index) => challenges[index].status === 200)
  if (!attempt && open.length !== accounts.length) {
    expectAll(challenges, [200], 'Login')
  }
  const codes = collectMail(
    open.map(({ email }) => ({ address: email, pattern: LOGIN_CODE, since })),
    { reuse: true },
  )
  const completed = parallel(
    open.map(({ session }, index) => ({
      session,
      method: 'POST',
      path: '/api/auth/login/two-factor',
      options: { name: 'POST /api/auth/login/two-factor', json: { code: codes[index][0] } },
    })),
  )
  expectAll(completed, [200], 'Second factor').forEach((user, index) => {
    open[index].session.user = user
    open[index].session.csrf = null
    open[index].codeAt = since
  })
  parallel(
    open.map(({ session }) => ({
      session,
      method: 'GET',
      path: '/api/auth/csrf',
      options: { name: 'GET /api/auth/csrf' },
    })),
  ).forEach((res, index) => (open[index].session.csrf = body(res)?.token ?? null))
  return open
}

function resetPasswords(accounts) {
  const since = Date.now()
  expectAll(
    parallel(
      accounts.map(({ session, email }) => ({
        session,
        method: 'POST',
        path: '/api/auth/forgot-password',
        options: { name: 'POST /api/auth/forgot-password', json: { email }, expect: [204] },
      })),
    ),
    [204],
    'Password recovery',
  )
  const links = collectMail(
    accounts.map(({ email }) => ({ address: email, pattern: RESET_LINK, since })),
  )
  expectAll(
    parallel(
      accounts.map(({ session }, index) => ({
        session,
        method: 'PATCH',
        path: `/api/auth/${links[index][0]}/reset-password`,
        options: {
          name: 'PATCH /api/auth/{userId}/reset-password',
          json: { otp: links[index][1], newPassword: PASSWORD },
          expect: [204],
        },
      })),
    ),
    [204],
    'Password reset',
  )
}

export function waitForApi(timeoutSeconds = 600) {
  const probe = new Session({ ip: clientIp('setup', 0) })
  const deadline = Date.now() + timeoutSeconds * 1000
  while (Date.now() < deadline) {
    const res = probe.get(`/api/events${query({ pageSize: 1 })}`, {
      name: 'GET /api/events [anonymous]',
    })
    if (res.status === 200 && body(res)?.total > 0) {
      return
    }
    sleep(2)
  }
  fail('The API never served the demo events')
}

export function bootstrapAdmin() {
  const account = {
    email: ADMIN_EMAIL,
    session: new Session({ ip: clientIp('setup', 1), actor: 'admin' }),
  }
  if (signIn([account], { attempt: true }).length) {
    return account
  }
  resetPasswords([account])
  signIn([account])
  return account
}

export function register(specs, admin) {
  const since = Date.now()
  const accounts = specs.map((spec) => ({
    ...spec,
    session: new Session({ ip: clientIp('account', spec.ipIndex), actor: spec.actor }),
  }))
  expectAll(
    parallel(
      accounts.map(({ session, email, minors = 0 }) => ({
        session,
        method: 'POST',
        path: '/api/auth/register',
        options: {
          name: 'POST /api/auth/register',
          json: registration(email, minors),
          expect: [204],
        },
      })),
    ),
    [204],
    'Registration',
  )
  const links = collectMail(
    accounts.map(({ email }) => ({ address: email, pattern: VERIFICATION_LINK, since })),
  )
  const verified = expectAll(
    parallel(
      accounts.map(({ session }, index) => ({
        session,
        method: 'PATCH',
        path: `/api/auth/${links[index][0]}/verify`,
        options: { name: 'PATCH /api/auth/{userId}/verify', json: { otp: links[index][1] } },
      })),
    ),
    [200],
    'Verification',
  )
  accounts.forEach((account, index) => (account.id = verified[index].id))
  for (const account of accounts.filter(({ grantAdmin }) => grantAdmin)) {
    admin.session.patch(`/api/users/${account.id}/admin`, {
      name: 'PATCH /api/users/{userId}/admin',
      json: { isAdmin: true, currentPassword: PASSWORD },
      expect: [204],
    })
  }
  for (const account of accounts.filter(({ userTypeId }) => userTypeId)) {
    admin.session.patch(
      `/api/users/${account.id}/change-type${query({ userTypeId: account.userTypeId })}`,
      {
        name: 'PATCH /api/users/{userId}/change-type',
      },
    )
  }
  return signIn(accounts)
}

export function takeOverDemoMembers(admin, count) {
  const res = admin.session.get(
    `/api/users${query({ scope: 'Independent', email: DEMO_EMAIL_DOMAIN, pageSize: 100 })}`,
    { name: 'GET /api/users' },
  )
  const demo = (body(res)?.items ?? []).filter((user) => !user.isAdmin).slice(0, count)
  if (demo.length < count) {
    fail(`Only ${demo.length} demo members found, ${count} needed`)
  }
  const accounts = demo.map((user, index) => ({
    email: user.email,
    session: new Session({ ip: clientIp('account', 1000 + index), actor: 'member' }),
  }))
  const signedIn = signIn(accounts, { attempt: true })
  const unknown = accounts.filter((account) => !signedIn.includes(account))
  if (unknown.length) {
    resetPasswords(unknown)
    signIn(unknown)
  }
  return accounts
}

export function household(session) {
  const res = session.get(`/api/users${query({ pageSize: 50 })}`, { name: 'GET /api/users' })
  return (body(res)?.items ?? [])
    .filter((user) => user.id !== session.userId)
    .map((user) => user.id)
}
