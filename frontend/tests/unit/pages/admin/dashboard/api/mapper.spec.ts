import { describe, expect, it } from 'vitest'

import { toDashboardAnalytics } from '@/pages/admin/dashboard/api/mapper'

import { buildDashboardAnalytics } from '../../../../../support/builders'

describe('toDashboardAnalytics', () => {
  it('maps every figure of the dashboard', () => {
    const analytics = toDashboardAnalytics(
      buildDashboardAnalytics({
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

    expect(analytics).toEqual({
      granularity: 'month',
      kpis: [
        { key: 'users', total: 1200, inRange: 30, previousRange: 20 },
        { key: 'members', total: 80, inRange: 2, previousRange: 4 },
        { key: 'inscriptions', total: 500, inRange: 10, previousRange: 10 },
        { key: 'events', total: 12, inRange: 0, previousRange: 0 },
      ],
      newUsers: {
        buckets: ['2026-08-01', '2026-09-01'],
        series: [{ key: 'member', values: [1, 2] }],
      },
      inscriptions: {
        buckets: ['2026-08-01', '2026-09-01'],
        series: [{ key: 'confirmed', values: [3, 4] }],
      },
      topEvents: [
        {
          id: 'event-1',
          title: 'Hackathon de primavera',
          confirmed: 42,
          seatsFilled: 30,
          seatsDesired: 40,
        },
      ],
      pendingSignups: {
        total: 3,
        events: [
          {
            id: 'event-1',
            title: 'Hackathon de primavera',
            pending: 3,
            oldestRequestedAt: '2026-09-01T10:00:00Z',
            nextActivityStartsAt: '2026-10-10T09:00:00Z',
          },
        ],
      },
      roleCoverage: [
        { key: 'leader', confirmed: 2, desired: 4 },
        { key: 'volunteer', confirmed: 0, desired: 0 },
        { key: 'participant', confirmed: 28, desired: 36 },
      ],
      participantProfile: {
        groups: ['0-5', '6-11', '12-17', 'adults'],
        series: [{ key: 'Female', values: [0, 2, 1, 5] }],
      },
      categoryDemand: [{ key: 'cat-1', label: 'Programación', color: '#ff6600', count: 3 }],
      recurrence: [
        { key: '1', label: null, color: null, count: 6 },
        { key: '2', label: null, color: null, count: 2 },
      ],
      satisfaction: {
        average: 4.5,
        scored: 2,
        events: [{ id: 'event-1', title: 'Hackathon de primavera', average: 4.5, scored: 2 }],
      },
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

  it('maps a missing average score to null', () => {
    const analytics = toDashboardAnalytics(
      buildDashboardAnalytics({ satisfaction: { scored: 0, events: [] } }),
    )

    expect(analytics.satisfaction).toEqual({ average: null, scored: 0, events: [] })
  })
})
