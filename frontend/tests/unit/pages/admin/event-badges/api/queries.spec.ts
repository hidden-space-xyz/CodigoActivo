import { describe, expect, it } from 'vitest'

import { badgeKeys, badgeQueries } from '@/pages/admin/event-badges/api/queries'

import { http, HttpResponse, server } from '../../../../../support/server'
import { buildBadge, EVENT_ID } from '../../../../../support/builders'
import { createTestQueryClient } from '../../../../../support/render'

describe('badgeQueries', () => {
  it('loads the badges of an event, keeping the guardian of minors', async () => {
    server.use(
      http.get('/api/reports/events/:eventId/badges', () =>
        HttpResponse.json({
          eventId: EVENT_ID,
          title: 'Hackathon',
          badges: [
            buildBadge(),
            buildBadge({
              userId: 'user-2',
              guardian: { firstName: 'Mary', lastName: 'Lovelace', phone: null },
            }),
          ],
        }),
      ),
    )

    const report = await createTestQueryClient().fetchQuery(badgeQueries.ofEvent(EVENT_ID))

    expect(report).toEqual({
      title: 'Hackathon',
      badges: [
        {
          userId: 'user-1',
          firstName: 'Ada',
          lastName: 'Lovelace',
          userTypeName: 'Member',
          userTypeColor: '#123456',
          guardian: null,
          activities: [{ title: 'Robotics', location: 'Lab 1' }],
        },
        expect.objectContaining({ userId: 'user-2', guardian: { firstName: 'Mary', phone: '' } }),
      ],
    })
    expect(badgeQueries.ofEvent(EVENT_ID).queryKey).toEqual(badgeKeys.ofEvent(EVENT_ID))
    expect(badgeKeys.ofEvent('e1')).toEqual(['reports', 'event-badges', 'e1'])
  })
})
