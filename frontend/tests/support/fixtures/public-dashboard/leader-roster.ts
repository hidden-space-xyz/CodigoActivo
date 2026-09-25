import type { LeaderRosterActivityResponse } from '@/shared/api/generated/models'

import { apiError, http, HttpResponse, server } from '../../server'

/**
 * Led activity as returned by `/api/events/:eventId/leader-roster`: a co-leader and the signed-in
 * leader, a volunteer, and participants split into one adult and two dependents.
 */
export function buildLeaderRosterActivity(
  overrides: LeaderRosterActivityResponse = {},
): LeaderRosterActivityResponse {
  return {
    activityId: 'act-1',
    title: 'Taller de robótica',
    location: 'Aula 3',
    activityStartsAt: '2099-06-10T09:00:00Z',
    activityEndsAt: '2099-06-10T11:00:00Z',
    roles: [
      {
        roleTypeId: 'role-leader',
        roleName: 'Líder',
        users: [
          {
            firstName: 'Luis',
            lastName: 'Lozano',
            email: 'luis@example.test',
            phone: '600 111 222',
            signedUpAt: '2099-05-01T10:00:00Z',
          },
          {
            firstName: 'Ada',
            lastName: 'Lovelace',
            email: 'ada@example.test',
            phone: '600 000 000',
            signedUpAt: '2099-05-02T10:00:00Z',
          },
        ],
        dependents: [],
      },
      {
        roleTypeId: 'role-volunteer',
        roleName: 'Voluntario',
        users: [
          {
            firstName: 'Víctor',
            lastName: 'Vega',
            email: '',
            phone: '',
            signedUpAt: '2099-05-03T10:00:00Z',
          },
        ],
        dependents: [],
      },
      {
        roleTypeId: 'role-participant',
        roleName: 'Participante',
        users: [
          {
            firstName: 'Ana',
            lastName: 'Álvarez',
            email: 'ana@example.test',
            phone: '600 333 444',
            signedUpAt: '2099-05-04T10:00:00Z',
          },
        ],
        dependents: [
          {
            firstName: 'Nora',
            lastName: 'Gil',
            age: 11,
            guardian: {
              firstName: 'Gabriela',
              lastName: 'Gil',
              email: 'gabriela@example.test',
              phone: '600 555 666',
            },
            signedUpAt: '2099-05-05T10:00:00Z',
          },
          {
            firstName: 'Hugo',
            lastName: 'Gil',
            age: 1,
            guardian: { firstName: 'Gabriela', lastName: 'Gil', email: '', phone: '' },
            signedUpAt: '2099-05-06T10:00:00Z',
          },
        ],
      },
    ],
    ...overrides,
  }
}

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
