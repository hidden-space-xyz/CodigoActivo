import { describe, expect, it } from 'vitest'

import { rosterKeys, rosterQueries } from '@/pages/admin/event-roster/api/queries'

import { http, HttpResponse, server } from '../../../../../support/server'
import {
  buildRosterActivity,
  buildRosterParticipant,
  EVENT_ID,
} from '../../../../../support/builders'
import { createTestQueryClient } from '../../../../../support/render'

describe('rosterQueries', () => {
  it('loads the roster of an event, leaving unknown contact data empty', async () => {
    server.use(
      http.get('/api/reports/events/:eventId/roster', () =>
        HttpResponse.json({
          eventId: EVENT_ID,
          title: 'Hackathon',
          activities: [
            buildRosterActivity({
              participants: [
                buildRosterParticipant({
                  email: null,
                  guardian: { firstName: 'Mary', lastName: 'Lovelace', phone: '611000000' },
                }),
              ],
            }),
          ],
        }),
      ),
    )

    const roster = await createTestQueryClient().fetchQuery(rosterQueries.ofEvent(EVENT_ID))

    expect(roster).toEqual({
      title: 'Hackathon',
      activities: [
        {
          id: 'act-1',
          title: 'Robotics',
          location: 'Room 1',
          startsAt: '2026-10-10T09:00:00Z',
          endsAt: '2026-10-10T11:00:00Z',
          participants: [
            {
              userId: 'user-1',
              firstName: 'Ada',
              lastName: 'Lovelace',
              birthDate: '2000-01-01',
              email: '',
              phone: '600000001',
              secondaryPhone: '',
              roleName: 'Monitora',
              guardian: {
                firstName: 'Mary',
                lastName: 'Lovelace',
                email: '',
                phone: '611000000',
                secondaryPhone: '',
              },
            },
          ],
        },
      ],
    })
    expect(rosterKeys.ofEvent('e1')).toEqual(['reports', 'event-roster', 'e1'])
  })
})
