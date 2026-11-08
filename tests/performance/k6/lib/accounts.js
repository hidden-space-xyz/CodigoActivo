import { fail, sleep } from 'k6'
import { ADMIN_EMAIL, DEMO_EMAIL_DOMAIN, PASSWORD } from './config.js'
import { body, clientIp, parallel, Session } from './http.js'
import { collectMail, LOGIN_CODE, RESET_LINK, VERIFICATION_LINK, waitForMail } from './mail.js'
import { registration } from './data.js'

const ADMIN_PACE_SECONDS = 0.25

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

function refreshCsrf(accounts) {
  each(accounts, 'GET', '/api/auth/csrf').forEach(
    (res, index) => (accounts[index].session.csrf = body(res).token),
  )
}

function signIn(accounts) {
  const since = Date.now()
  each(accounts, 'POST', '/api/auth/login', ({ email }) => ({
    json: { identifier: email, password: PASSWORD, keepSignedIn: true },
  }))
  const codes = collectMail(
    accounts.map(({ email }) => ({ address: email, pattern: LOGIN_CODE, since })),
  )
  each(accounts, 'POST', '/api/auth/login/two-factor', (_, index) => ({
    json: { code: codes[index][0] },
  })).forEach((res, index) => (accounts[index].session.user = body(res)))
  refreshCsrf(accounts)
  return accounts
}

function takeOver(accounts) {
  const since = Date.now()
  each(accounts, 'POST', '/api/auth/forgot-password', ({ email }) => ({
    json: { email },
    expect: [204],
  }))
  const links = collectMail(
    accounts.map(({ email }) => ({ address: email, pattern: RESET_LINK, since })),
  )
  each(accounts, 'PATCH', '/api/auth/{userId}/reset-password', (_, index) => ({
    route: { userId: links[index][0] },
    json: { otp: links[index][1], newPassword: PASSWORD },
    expect: [204],
  }))
  return signIn(accounts)
}

/** Signs in the initial administrator, who starts with a discarded random password. */
export function bootstrapAdmin() {
  const [admin] = takeOver([
    { email: ADMIN_EMAIL, session: new Session({ ip: clientIp('setup', 1), actor: 'admin' }) },
  ])
  return admin
}

/** Registers, verifies and signs in one account per spec, with the type and staff role it asks. */
export function register(specs, admin) {
  const since = Date.now()
  const accounts = specs.map((spec) => ({
    ...spec,
    session: new Session({ ip: clientIp('account', spec.ipIndex), actor: spec.actor }),
  }))
  each(accounts, 'POST', '/api/auth/register', ({ email, minors = 0 }) => ({
    json: registration(email, minors),
    expect: [204],
  }))
  const links = collectMail(
    accounts.map(({ email }) => ({ address: email, pattern: VERIFICATION_LINK, since })),
  )
  each(accounts, 'PATCH', '/api/auth/{userId}/verify', (_, index) => ({
    route: { userId: links[index][0] },
    json: { otp: links[index][1] },
    expect: [204],
  }))
  links.forEach(([userId], index) => (accounts[index].id = userId))
  for (const { id, grantAdmin, userTypeId } of accounts) {
    admin.session.patch('/api/users/{userId}/change-type', {
      route: { userId: id },
      query: { userTypeId },
    })
    sleep(ADMIN_PACE_SECONDS)
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

/** Takes over active demo members, whose history the rating and certificate pages need. */
export function takeOverDemoMembers(admin, count, firstIpIndex) {
  const { items } = body(
    admin.session.get('/api/users', {
      query: { scope: 'Independent', email: DEMO_EMAIL_DOMAIN, pageSize: 100 },
    }),
  )
  const demo = items
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

/**
 * Completes a login the way the SPA does: password, second-factor page, emailed code. The SPA
 * drops its token after a login, so the next unsafe request fetches a new one. Tells whether it
 * succeeded.
 */
export function logIn(session, address) {
  const since = Date.now()
  const challenge = session.post('/api/auth/login', {
    json: { identifier: address, password: PASSWORD, keepSignedIn: false },
  })
  if (challenge.status !== 200) {
    return false
  }
  session.get('/api/auth/login/two-factor')
  const [code] = waitForMail(address, LOGIN_CODE, since)
  const completed = session.post('/api/auth/login/two-factor', { json: { code } })
  session.csrf = null
  session.user = body(completed)
  return completed.status === 200
}
