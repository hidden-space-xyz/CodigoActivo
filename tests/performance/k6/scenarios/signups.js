import { think } from '../lib/profiles.js'
import { nextAccount, sessionFor } from '../lib/pool.js'
import { decisionsFor, LOOKUPS } from '../lib/data.js'
import { eventActivities, eventDetail, eventTerms, signupState } from '../lib/pages.js'

const WITHDRAWAL_EVERY = 100

function signUp(session, event, activityId, request) {
  session.get('/api/activities/{activityId}/overlaps/{userId}', {
    route: { activityId, userId: session.userId },
  })
  request()
  signupState(session, event.id)
  eventTerms(session, event)
}

export function signups(data) {
  const { account, round, iteration } = nextAccount(data.members)
  const session = sessionFor(account)
  const { rushEventId, consentId, activities } = data.fixtures
  const open = activities.slice(1)
  const single = open[(round * 2) % open.length]
  const shared = open[(round * 2 + 1) % open.length]
  const people = [session.userId, ...account.dependents]

  const event = eventDetail(session, rushEventId)
  think()
  const termsDecisions = decisionsFor(eventActivities(session, event).documents)
  const participant = { activityRoleTypeId: LOOKUPS.roles.participant, termsDecisions }
  think()

  signUp(session, event, single, () =>
    session.patch('/api/activities/{activityId}/{userId}/assign', {
      route: { activityId: single, userId: session.userId },
      json: participant,
    }),
  )
  think()

  signUp(session, event, shared, () =>
    session.post('/api/activities/{activityId}/assign-household', {
      route: { activityId: shared },
      json: { assignments: people.map((userId) => ({ userId, ...participant })) },
    }),
  )
  think()

  if (iteration % WITHDRAWAL_EVERY === 0) {
    session.post('/api/events/{eventId}/terms/decisions/{userId}/{termsDocumentId}/withdraw', {
      route: { eventId: rushEventId, userId: session.userId, termsDocumentId: consentId },
      expect: [204],
    })
  }

  for (const [activityId, userIds] of [
    [single, [session.userId]],
    [shared, people],
  ]) {
    for (const userId of userIds) {
      session.patch('/api/activities/{activityId}/{userId}/unassign', {
        route: { activityId, userId },
        expect: [204],
      })
    }
  }
  signupState(session, rushEventId)
  think()
}
