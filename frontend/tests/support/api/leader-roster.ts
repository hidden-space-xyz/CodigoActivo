import type { LeaderRosterActivityResponse } from '@/shared/api/generated/models'

import { apiError, http, HttpResponse, server } from '../server'

/**
 * Serves the leader roster of any event; `'error'` answers 500. Returns the list of requested
 * event ids so tests can check whether (and for which event) the roster was fetched.
 */
export function serveLeaderRoster(response: LeaderRosterActivityResponse[] | 'error' = []) {
  const requests: string[] = []
  server.use(
    http.get('/api/events/:eventId/leader-roster', ({ params }) => {
      requests.push(String(params.eventId))
      return response === 'error' ? apiError(500) : HttpResponse.json(response)
    }),
  )
  return requests
}
