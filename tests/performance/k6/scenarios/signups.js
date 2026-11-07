import { think } from '../lib/profiles.js'
import { nextAccount, sessionFor } from '../lib/pool.js'
import { decisionsFor, LOOKUPS } from '../lib/data.js'
import { eventActivities, eventDetail, eventTerms, signupState } from '../lib/pages.js'

const WITHDRAWAL_EVERY = 100

function enrol(session, activityId, dependents, participant) {
  if (dependents.length) {
    session.post('/api/activities/{activityId}/assign-household', {
      route: { activityId },
      json: {
        assignments: [session.userId, ...dependents].map((userId) => ({ userId, ...participant })),
      },
    })
    return
  }
  const route = { activityId, userId: session.userId }
  session.get('/api/activities/{activityId}/overlaps/{userId}', { route })
  session.patch('/api/activities/{activityId}/{userId}/assign', { route, json: participant })
}

function leave(session, event, activityId, userId) {
  session.patch('/api/activities/{activityId}/{userId}/unassign', {
    route: { activityId, userId },
    expect: [204],
  })
  signupState(session, event.id)
}

function withdrawConsent(session, eventId, termsDocumentId) {
  session.post('/api/events/{eventId}/terms/decisions/{userId}/{termsDocumentId}/withdraw', {
    route: { eventId, userId: session.userId, termsDocumentId },
    expect: [204],
  })
  session.get('/api/me/consents')
}

/**
 * An account signs up for two activities of the shared event and leaves them. The SPA signs up an
 * account with children through the household dialog, and one without them after checking overlaps.
 */
export function signups(data) {
  const { account, round, iteration } = nextAccount(data.members)
  const session = sessionFor(account)
  const { rushEventId, consentId, activities } = data.fixtures
  const open = activities.slice(1)
  const chosen = [open[(round * 2) % open.length], open[(round * 2 + 1) % open.length]]

  const event = eventDetail(session, rushEventId)
  think()
  const terms = event && eventActivities(session, event)
  think()
  if (!terms) {
    return
  }
  const participant = {
    activityRoleTypeId: LOOKUPS.roles.participant,
    termsDecisions: decisionsFor(terms.documents),
  }

  for (const activityId of chosen) {
    enrol(session, activityId, account.dependents, participant)
    signupState(session, rushEventId)
    eventTerms(session, event)
    think()
  }

  if (iteration % WITHDRAWAL_EVERY === 0) {
    withdrawConsent(session, rushEventId, consentId)
  }

  for (const activityId of chosen) {
    for (const userId of [session.userId, ...account.dependents]) {
      leave(session, event, activityId, userId)
    }
  }
  think()
}
