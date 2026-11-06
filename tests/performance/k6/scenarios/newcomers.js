import exec from 'k6/execution'
import { PASSWORD } from '../lib/config.js'
import { body, clientIp, Session } from '../lib/http.js'
import { think } from '../lib/profiles.js'
import { LOGIN_CODE, VERIFICATION_LINK, waitForMail } from '../lib/mail.js'
import { email, registration } from '../lib/data.js'
import { accountProfile } from '../lib/pages.js'

export function newcomers(data) {
  const iteration = exec.scenario.iterationInTest
  const address = email('familia', data.runId, iteration)
  const session = new Session({ ip: clientIp('newcomer', iteration), actor: 'newcomer' })

  let since = Date.now()
  session.post('/api/auth/register', {
    json: registration(address, 1 + (iteration % 2)),
    expect: [204],
  })
  const [userId, otp] = waitForMail(address, VERIFICATION_LINK, { since })
  think()

  session.patch('/api/auth/{userId}/verify', { route: { userId }, json: { otp }, expect: [204] })
  think()

  since = Date.now()
  session.post('/api/auth/login', {
    json: { identifier: address, password: PASSWORD, keepSignedIn: false },
  })
  session.get('/api/auth/login/two-factor')
  const [code] = waitForMail(address, LOGIN_CODE, { since })
  session.user = body(session.post('/api/auth/login/two-factor', { json: { code } }))
  think()

  accountProfile(session)
  think()
}
