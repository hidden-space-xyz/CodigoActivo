import { describe, expect, it } from 'vitest'

import { toDashboardAnalytics } from '@/pages/admin/dashboard/api/mapper'

import { buildDashboardAnalytics } from '../../../../../support/builders'

describe('toDashboardAnalytics', () => {
  it('maps every figure of the dashboard', () => {
    const analytics = toDashboardAnalytics(
      buildDashboardAnalytics({
        usersByType: [{ key: 'member', count: 10 }],
        occupancy: {
          confirmed: 30,
          desired: 40,
          events: [
            {
              eventId: 'event-1',
              title: 'Hackathon',
              confirmed: 30,
              desired: 40,
              activities: [
                {
                  activityId: 'act-1',
                  title: 'Robotics',
                  startsAt: '2026-10-10T09:00:00Z',
                  confirmed: 12,
                  desired: 10,
                },
              ],
            },
          ],
        },
      }),
    )

    expect(analytics).toMatchObject({
      granularity: 'month',
      kpis: [
        { key: 'users', total: 1200, inRange: 30, previousRange: 20 },
        { key: 'members', total: 80, inRange: 2, previousRange: 4 },
        { key: 'inscriptions', total: 500, inRange: 10, previousRange: 10 },
        { key: 'events', total: 12, inRange: 0, previousRange: 0 },
      ],
      userGrowth: {
        buckets: ['2026-08-01', '2026-09-01'],
        series: [{ key: 'member', values: [1, 2] }],
      },
      usersByType: [{ key: 'member', label: null, color: null, count: 10 }],
      eventsByCategory: [{ key: 'cat-1', label: 'Programación', color: '#ff6600', count: 3 }],
      topEvents: [{ id: 'event-1', title: 'Hackathon de primavera', confirmed: 42 }],
      occupancy: {
        confirmed: 30,
        desired: 40,
        events: [
          {
            id: 'event-1',
            title: 'Hackathon',
            confirmed: 30,
            desired: 40,
            activities: [
              {
                id: 'act-1',
                title: 'Robotics',
                startsAt: '2026-10-10T09:00:00Z',
                confirmed: 12,
                desired: 10,
              },
            ],
          },
        ],
      },
    })
  })
})
