import { body } from '../lib/http.js'
import { think } from '../lib/profiles.js'
import { nextAccount, sessionFor } from '../lib/pool.js'
import { pick, randomInt, word } from '../lib/data.js'
import { accountProfile, eventActivities, eventDetail, home, openSite } from '../lib/pages.js'

function rate(session, history, iteration) {
  const rateable = history.filter((entry) => entry.canRate)
  if (!rateable.length) {
    return
  }
  const route = { eventId: pick(rateable, iteration).eventId }
  if (iteration % 4 === 1) {
    session.delete('/api/events/{eventId}/rating', { route, expect: [204, 404] })
    return
  }
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
}

export function members(data) {
  const { account, iteration } = nextAccount(data.demoMembers.concat(data.members))
  const session = sessionFor(account)
  openSite(session)
  const upcoming = home(session)
  think()

  const eventId = iteration % 2 || !upcoming.length ? data.fixtures.rushEventId : pick(upcoming).id
  const event = eventDetail(session, eventId)
  think()
  eventActivities(session, event)
  think()

  accountProfile(session)
  think()

  const history = body(session.get('/api/me/event-history')) ?? []
  rate(session, history, iteration)
  think()

  session.get('/api/me/certificates')
  think()
}
