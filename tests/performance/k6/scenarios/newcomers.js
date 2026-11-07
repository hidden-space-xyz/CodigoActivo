import exec from 'k6/execution'
import { logIn } from '../lib/accounts.js'
import { clientIp, Session } from '../lib/http.js'
import { think } from '../lib/profiles.js'
import { VERIFICATION_LINK, waitForMail } from '../lib/mail.js'
import { email, registration } from '../lib/data.js'
import { accountProfile, home, openSite } from '../lib/pages.js'

/** A new family registers, opens the link of the email, signs in and reaches the account page. */
export function newcomers(data) {
  const iteration = exec.scenario.iterationInTest
  const address = email('familia', data.runId, iteration)
  const session = new Session({ ip: clientIp('newcomer', iteration), actor: 'newcomer' })

  session.get('/api/legal-entity')
  const since = Date.now()
  const registered = session.post('/api/auth/register', {
    json: registration(address, 1 + (iteration % 2)),
    expect: [204],
  })
  if (registered.status !== 204) {
    return
  }
  const [userId, otp] = waitForMail(address, VERIFICATION_LINK, since)
  think()

  openSite(session)
  const verified = session.patch('/api/auth/{userId}/verify', {
    route: { userId },
    json: { otp },
    expect: [204],
  })
  think()

  if (verified.status === 204 && logIn(session, address)) {
    home(session)
    think()
    accountProfile(session)
    think()
  }
}
