import { body, query, think } from '../lib/http.js'
import { nextAccount, sessionFor } from '../lib/pool.js'
import { CATALOG, decisionsFor } from '../lib/data.js'

const WITHDRAWAL_EVERY = 100

export function signup(data) {
  const { account, round, iteration } = nextAccount(data.members)
  const session = sessionFor(account)
  const { rushEventId, consentId, activities } = data.fixtures
  const open = activities.slice(1)
  const single = open[(round * 2) % open.length]
  const shared = open[(round * 2 + 1) % open.length]
  const people = [session.userId, ...account.dependents]

  session.get(`/api/events/${rushEventId}`, { name: 'GET /api/events/{eventId}' })
  session.get(`/api/activities${query({ eventId: rushEventId, pageSize: 50 })}`, {
    name: 'GET /api/activities',
  })
  const terms = body(
    session.get(`/api/events/${rushEventId}/terms`, { name: 'GET /api/events/{eventId}/terms' }),
  )
  session.get('/api/activities/signup-roles', { name: 'GET /api/activities/signup-roles' })
  session.get(`/api/activities/household-assignments/${rushEventId}`, {
    name: 'GET /api/activities/household-assignments/{eventId}',
  })
  think()

  const decisions = decisionsFor(terms.documents)
  session.get(`/api/activities/${single}/overlaps/${session.userId}`, {
    name: 'GET /api/activities/{activityId}/overlaps/{userId}',
  })
  session.patch(`/api/activities/${single}/${session.userId}/assign`, {
    name: 'PATCH /api/activities/{activityId}/{userId}/assign',
    json: { activityRoleTypeId: CATALOG.roles.participant, termsDecisions: decisions },
  })
  session.post(`/api/activities/${shared}/assign-household`, {
    name: 'POST /api/activities/{activityId}/assign-household',
    json: {
      assignments: people.map((userId) => ({
        userId,
        activityRoleTypeId: CATALOG.roles.participant,
        termsDecisions: decisions,
      })),
    },
  })
  session.get(`/api/me/assigned-activities${query({ eventId: rushEventId })}`, {
    name: 'GET /api/me/assigned-activities',
  })
  think()

  if (iteration % WITHDRAWAL_EVERY === 0) {
    session.post(
      `/api/events/${rushEventId}/terms/decisions/${session.userId}/${consentId}/withdraw`,
      {
        name: 'POST /api/events/{eventId}/terms/decisions/{userId}/{termsDocumentId}/withdraw',
        expect: [204],
      },
    )
    think()
  }

  session.patch(`/api/activities/${single}/${session.userId}/unassign`, {
    name: 'PATCH /api/activities/{activityId}/{userId}/unassign',
    expect: [204],
  })
  for (const userId of people) {
    session.patch(`/api/activities/${shared}/${userId}/unassign`, {
      name: 'PATCH /api/activities/{activityId}/{userId}/unassign',
      expect: [204],
    })
  }
}
