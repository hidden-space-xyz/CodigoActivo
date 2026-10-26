import { body, query, think } from '../lib/http.js'
import { nextAccount, sessionFor } from '../lib/pool.js'
import { CATALOG, day, pick, word } from '../lib/data.js'

export function reports(data) {
  const { account, iteration } = nextAccount(data.admins.reports)
  const session = sessionFor(account)
  const { rushEventId, pastEvents } = data.fixtures
  const eventId = iteration % 2 ? rushEventId : pick(pastEvents)

  session.get('/api/reports/dashboard', { name: 'GET /api/reports/dashboard' })
  session.get(`/api/reports/dashboard/analytics${query({ from: day(-365), to: day(0) })}`, {
    name: 'GET /api/reports/dashboard/analytics',
  })
  think()

  session.get(`/api/reports/events/${eventId}/summary`, {
    name: 'GET /api/reports/events/{eventId}/summary',
  })
  session.get(`/api/reports/events/${eventId}/attendees${query({ pageSize: 25 })}`, {
    name: 'GET /api/reports/events/{eventId}/attendees',
  })
  session.get(`/api/reports/events/${eventId}/badges`, {
    name: 'GET /api/reports/events/{eventId}/badges',
  })
  session.get(`/api/reports/events/${eventId}/roster`, {
    name: 'GET /api/reports/events/{eventId}/roster',
  })
  session.get(
    `/api/emails/events/${eventId}/attendees/audience${query({ statusId: CATALOG.statuses.confirmed })}`,
    {
      name: 'GET /api/emails/events/{eventId}/attendees/audience',
    },
  )
  session.get(`/api/emails/users/audience${query({ scope: 'Independent', newsConsent: true })}`, {
    name: 'GET /api/emails/users/audience',
  })
  think()

  const diagnostics = body(
    session.get(`/api/logs/diagnostics${query({ pageSize: 25 })}`, {
      name: 'GET /api/logs/diagnostics',
    }),
  ).items
  if (diagnostics.length) {
    session.get(`/api/logs/diagnostics/${pick(diagnostics).id}`, {
      name: 'GET /api/logs/diagnostics/{id}',
    })
  }
  session.get(`/api/logs/diagnostics${query({ search: word(), level: 'Warning', pageSize: 25 })}`, {
    name: 'GET /api/logs/diagnostics',
  })
  think()

  session.get(`/api/logs/audit${query({ pageSize: 25 })}`, { name: 'GET /api/logs/audit' })
  session.get(
    `/api/logs/audit${query({ action: 'CompleteLogin', occurredFrom: day(-7), pageSize: 25 })}`,
    { name: 'GET /api/logs/audit' },
  )
  session.get(`/api/logs/audit${query({ search: word(), outcome: 'Rejected', pageSize: 25 })}`, {
    name: 'GET /api/logs/audit',
  })
}
