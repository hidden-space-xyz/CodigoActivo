import { logIn } from '../lib/accounts.js'
import { Session } from '../lib/http.js'
import { think } from '../lib/profiles.js'
import { nextAccount } from '../lib/pool.js'
import { home, openSite } from '../lib/pages.js'

/**
 * A member whose session ended signs in and lands on the home page. It opens a session of its own,
 * so the one the other scenarios share for the account stays open.
 */
export function logins(data) {
  const { account } = nextAccount(data.demoMembers.concat(data.members))
  const session = new Session({ ip: account.session.ip, actor: 'member' })
  openSite(session)
  if (logIn(session, account.email)) {
    home(session)
  }
  think()
}
