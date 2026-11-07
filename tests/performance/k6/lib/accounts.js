import { fail, sleep } from 'k6'
import { ADMIN_EMAIL, DEMO_EMAIL_DOMAIN, PASSWORD } from './config.js'
import { body, clientIp, parallel, Session } from './http.js'
import { collectMail, LOGIN_CODE, RESET_LINK, VERIFICATION_LINK } from './mail.js'
import { registration } from './data.js'

function each(accounts, method, path, options = () => ({})) {
  return parallel(
    accounts.map((account, index) => ({
      session: account.session,
      method,
      path,
      options: options(account, index),
    })),
  )
}

function expectAll(responses, statuses, what) {
  const failed = responses.filter((res) => !statuses.includes(res.status))
  if (failed.length) {
    fail(`${what} failed for ${failed.length} account(s): ${failed[0].status} ${failed[0].body}`)
  }
  return responses.map(body)
}

function refreshCsrf(accounts) {
  each(accounts, 'GET', '/api/auth/csrf').forEach(
    (res, index) => (accounts[index].session.csrf = body(res)?.token ?? null),
  )
}

function signIn(accounts, { attempt = false } = {}) {
  refreshCsrf(accounts)
  const since = Date.now()
  const challenges = each(accounts, 'POST', '/api/auth/login', ({ email }) => ({
    json: { identifier: email, password: PASSWORD, keepSignedIn: true },
    expect: attempt ? [200, 401] : [200],
  }))
  if (!attempt) {
    expectAll(challenges, [200], 'Login')
  }
  const open = accounts.filter((_, index) => challenges[index].status === 200)
  const codes = collectMail(
    open.map(({ email }) => ({ address: email, pattern: LOGIN_CODE, since })),
    { reuse: true },
  )
  const users = expectAll(
    each(open, 'POST', '/api/auth/login/two-factor', (_, index) => ({
      json: { code: codes[index][0] },
    })),
    [200],
    'Second factor',
  )
  open.forEach(({ session }, index) => (session.user = users[index]))
  refreshCsrf(open)
  return open
}

function resetPasswords(accounts) {
  const since = Date.now()
  expectAll(
    each(accounts, 'POST', '/api/auth/forgot-password', ({ email }) => ({
      json: { email },
      expect: [204],
    })),
    [204],
    'Password recovery',
  )
  const links = collectMail(
    accounts.map(({ email }) => ({ address: email, pattern: RESET_LINK, since })),
  )
  expectAll(
    each(accounts, 'PATCH', '/api/auth/{userId}/reset-password', (_, index) => ({
      route: { userId: links[index][0] },
      json: { otp: links[index][1], newPassword: PASSWORD },
      expect: [204],
    })),
    [204],
    'Password reset',
  )
}

function takeOver(accounts) {
  const signedIn = signIn(accounts, { attempt: true })
  const locked = accounts.filter((account) => !signedIn.includes(account))
  if (locked.length) {
    resetPasswords(locked)
    signIn(locked)
  }
  return accounts
}

export function waitForApi(timeoutSeconds = 600) {
  const probe = new Session({ ip: clientIp('setup', 0) })
  const deadline = Date.now() + timeoutSeconds * 1000
  while (Date.now() < deadline) {
    const res = probe.get('/api/events', { query: { pageSize: 1 } })
    if (res.status === 200 && body(res)?.total > 0) {
      return
    }
    sleep(2)
  }
  fail('The API never served the demo events')
}

export function bootstrapAdmin() {
  const [admin] = takeOver([
    { email: ADMIN_EMAIL, session: new Session({ ip: clientIp('setup', 1), actor: 'admin' }) },
  ])
  return admin
}

export function register(specs, admin) {
  const since = Date.now()
  const accounts = specs.map((spec) => ({
    ...spec,
    session: new Session({ ip: clientIp('account', spec.ipIndex), actor: spec.actor }),
  }))
  expectAll(
    each(accounts, 'POST', '/api/auth/register', ({ email, minors = 0 }) => ({
      json: registration(email, minors),
      expect: [204],
    })),
    [204],
    'Registration',
  )
  const links = collectMail(
    accounts.map(({ email }) => ({ address: email, pattern: VERIFICATION_LINK, since })),
  )
  expectAll(
    each(accounts, 'PATCH', '/api/auth/{userId}/verify', (_, index) => ({
      route: { userId: links[index][0] },
      json: { otp: links[index][1] },
      expect: [204],
    })),
    [204],
    'Verification',
  )
  links.forEach(([userId], index) => (accounts[index].id = userId))
  for (const { id, grantAdmin, userTypeId } of accounts) {
    if (userTypeId) {
      admin.session.patch('/api/users/{userId}/change-type', {
        route: { userId: id },
        query: { userTypeId },
      })
    }
    if (grantAdmin) {
      admin.session.patch('/api/users/{userId}/staff-role', {
        route: { userId: id },
        json: { staffRole: 'Administrator', currentPassword: PASSWORD },
        expect: [204],
      })
    }
  }
  return signIn(accounts)
}

export function takeOverDemoMembers(admin, count, firstIpIndex) {
  const found = body(
    admin.session.get('/api/users', {
      query: { scope: 'Independent', email: DEMO_EMAIL_DOMAIN, pageSize: 100 },
    }),
  )
  const demo = (found?.items ?? [])
    .filter((user) => user.staffRole === 'None' && user.status.name === 'Activo')
    .slice(0, count)
  if (demo.length < count) {
    fail(`Only ${demo.length} demo members found, ${count} needed`)
  }
  return takeOver(
    demo.map((user, index) => ({
      email: user.email,
      session: new Session({ ip: clientIp('account', firstIpIndex + index), actor: 'member' }),
    })),
  )
}
