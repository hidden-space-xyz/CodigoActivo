import { body, query, think } from '../lib/http.js'
import { nextAccount, sessionFor } from '../lib/pool.js'
import { pick, published, randomInt, word } from '../lib/data.js'

export function member(data) {
  const { account, iteration } = nextAccount(data.demoMembers.concat(data.members))
  const session = sessionFor(account)
  session.get('/api/auth/me', { name: 'GET /api/auth/me' })
  const upcoming = published(
    body(
      session.get(`/api/events${query({ scope: 'Upcoming', pageSize: 12 })}`, {
        name: 'GET /api/events',
      }),
    ).items,
  )
  session.get(`/api/news${query({ pageSize: 6 })}`, { name: 'GET /api/news' })
  think()

  const eventId = iteration % 2 || !upcoming.length ? data.fixtures.rushEventId : pick(upcoming).id
  session.get(`/api/events/${eventId}`, { name: 'GET /api/events/{eventId}' })
  const activities = body(
    session.get(`/api/activities${query({ eventId, pageSize: 50 })}`, {
      name: 'GET /api/activities',
    }),
  ).items
  session.get(`/api/events/${eventId}/terms`, { name: 'GET /api/events/{eventId}/terms' })
  session.get(`/api/activities/household-assignments/${eventId}`, {
    name: 'GET /api/activities/household-assignments/{eventId}',
  })
  session.get('/api/activities/signup-roles', { name: 'GET /api/activities/signup-roles' })
  if (activities.length) {
    const activity = pick(activities)
    session.get(`/api/activities/${activity.id}`, { name: 'GET /api/activities/{activityId}' })
    session.get(`/api/activities/${activity.id}/overlaps/${session.userId}`, {
      name: 'GET /api/activities/{activityId}/overlaps/{userId}',
    })
  }
  session.get(`/api/events/${eventId}/leader-roster`, {
    name: 'GET /api/events/{eventId}/leader-roster',
  })
  think()

  session.get('/api/me/assigned-activities', { name: 'GET /api/me/assigned-activities' })
  session.get(`/api/me/assigned-activities${query({ eventId })}`, {
    name: 'GET /api/me/assigned-activities',
  })
  const history = body(session.get('/api/me/event-history', { name: 'GET /api/me/event-history' }))
  session.get('/api/me/certificates', { name: 'GET /api/me/certificates' })
  session.get('/api/me/consents', { name: 'GET /api/me/consents' })
  session.get('/api/me/deletion', { name: 'GET /api/me/deletion' })
  session.get(`/api/users${query({ pageSize: 50 })}`, { name: 'GET /api/users' })
  think()

  const rateable = history.filter((entry) => entry.canRate)
  if (rateable.length) {
    const entry = pick(rateable, iteration)
    if (iteration % 4 === 1) {
      session.delete(`/api/events/${entry.eventId}/rating`, {
        name: 'DELETE /api/events/{eventId}/rating',
        expect: [204, 404],
      })
    } else {
      session.put(`/api/events/${entry.eventId}/rating`, {
        name: 'PUT /api/events/{eventId}/rating',
        json: {
          score: randomInt(3, 5),
          mostLiked: `Los talleres de ${word()}`,
          leastLiked: null,
          suggestions: `Más sesiones de ${word()}`,
        },
        expect: [204],
      })
    }
    think()
  }

  session.get('/api/me/personal-data', { name: 'GET /api/me/personal-data' })
  session.get(`/api/resources${query({ pageSize: 12 })}`, { name: 'GET /api/resources' })
  session.get(`/api/partners${query({ pageSize: 50 })}`, { name: 'GET /api/partners' })
  session.get(`/api/events${query({ search: word(), pageSize: 12 })}`, { name: 'GET /api/events' })
}
