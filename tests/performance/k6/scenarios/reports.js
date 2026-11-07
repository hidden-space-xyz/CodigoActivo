import { think } from '../lib/profiles.js'
import { nextAccount, sessionFor } from '../lib/pool.js'
import { body } from '../lib/http.js'
import { day, pick, word } from '../lib/data.js'

const LATEST = { sort: '-occurredAt', page: 1, pageSize: 25 }

/** An administrator reads the dashboard, the reports of an event and the logs. */
export function reports(data) {
  const { account, iteration } = nextAccount(data.admins.reports)
  const session = sessionFor(account)
  const { rushEventId, pastEvents } = data.fixtures
  const route = { eventId: iteration % 2 ? rushEventId : pick(pastEvents) }

  session.get('/api/reports/dashboard/analytics', { query: { from: day(-365), to: day(0) } })
  think()

  session.get('/api/reports/events/{eventId}/summary', { route })
  session.get('/api/reports/events/{eventId}/attendees', {
    route,
    query: { page: 1, pageSize: 25, sort: 'firstName' },
  })
  think()
  session.get('/api/reports/events/{eventId}/badges', { route })
  session.get('/api/reports/events/{eventId}/roster', { route })
  think()

  const diagnostics = body(session.get('/api/logs/diagnostics', { query: LATEST }))?.items ?? []
  if (diagnostics.length) {
    session.get('/api/logs/diagnostics/{id}', { route: { id: pick(diagnostics).id } })
  }
  session.get('/api/logs/diagnostics', { query: { ...LATEST, search: word(), level: 'Warning' } })
  think()

  const auditor = sessionFor(data.auditor)
  auditor.get('/api/logs/audit', { query: LATEST })
  auditor.get('/api/logs/audit', {
    query: { ...LATEST, action: 'CompleteLogin', occurredFrom: day(-7) },
  })
  auditor.get('/api/logs/audit', { query: { ...LATEST, search: word(), outcome: 'Rejected' } })
  think()
}
