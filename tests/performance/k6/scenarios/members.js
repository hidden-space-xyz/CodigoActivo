import { body } from '../lib/http.js'
import { think } from '../lib/profiles.js'
import { nextAccount, sessionFor } from '../lib/pool.js'
import { pick, randomInt, word } from '../lib/data.js'
import { accountProfile, eventActivities, eventDetail, home, openSite } from '../lib/pages.js'

const REMOVED_RATING_EVERY = 4

function rate(session, history, iteration) {
  const rateable = history.filter((entry) => entry.canRate)
  if (!rateable.length) {
    return
  }
  const route = { eventId: pick(rateable, iteration).eventId }
  session.put('/api/events/{eventId}/rating', {
    route,
    json: {
      score: randomInt(3, 5),
      mostLiked: `Los talleres de ${word()}`,
      leastLiked: null,
      suggestions: `Más sesiones de ${word()}`,
    },
    expect: [204],
  })
  session.get('/api/me/event-history')
  if (iteration % REMOVED_RATING_EVERY === 1) {
    session.delete('/api/events/{eventId}/rating', { route, expect: [204] })
    session.get('/api/me/event-history')
  }
}

/** A signed-in visit: home, an event with its activities, the account, history and certificates. */
export function members(data) {
  const { account, iteration } = nextAccount(data.demoMembers.concat(data.members))
  const session = sessionFor(account)
  openSite(session)
  const upcoming = home(session)
  think()

  const eventId = iteration % 2 || !upcoming.length ? data.fixtures.rushEventId : pick(upcoming).id
  const event = eventDetail(session, eventId)
  think()
  if (event) {
    eventActivities(session, event)
    think()
  }

  accountProfile(session)
  think()

  rate(session, body(session.get('/api/me/event-history')) ?? [], iteration)
  think()

  session.get('/api/me/certificates')
  think()
}
