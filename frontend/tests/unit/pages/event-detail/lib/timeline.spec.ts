import { describe, expect, it } from 'vitest'

import type { EventActivity } from '@/entities/activity'
import { toTimeline, toTimelineActivities } from '@/pages/event-detail/lib/timeline'
import type { TimelineActivity } from '@/pages/event-detail/model/types'

const NOW = new Date('2026-01-01T00:00:00Z')

function eventActivity(overrides: Partial<EventActivity> = {}): EventActivity {
  return {
    id: 'activity-1',
    title: 'Robótica',
    description: '<p>Construye un robot</p>',
    location: 'Aula 1',
    modality: 'Presencial',
    startsAt: '2099-06-10T09:00:00Z',
    endsAt: '2099-06-10T11:00:00Z',
    highDemandRoleIds: ['role-participant'],
    ...overrides,
  }
}

function timelineActivity(id: string, start: string, end: string): TimelineActivity {
  return {
    id,
    title: id,
    description: '',
    location: '',
    modality: '',
    start: new Date(start),
    end: new Date(end),
    started: false,
    highDemandRoleIds: [],
    assignment: null,
    household: [],
  }
}

describe('toTimelineActivities', () => {
  it('merges each activity with the signups of the user and their household', () => {
    const [first, second] = toTimelineActivities(
      [eventActivity(), eventActivity({ id: 'activity-2', title: 'Python' })],
      [{ activityId: 'activity-1', status: 'Confirmada', roleName: 'Mentor' }],
      [
        {
          activityId: 'activity-1',
          userId: 'child-1',
          name: 'Byron King',
          roleName: 'Participante',
          status: 'Solicitada',
        },
      ],
      true,
      NOW,
    )

    expect(first).toEqual({
      id: 'activity-1',
      title: 'Robótica',
      description: '<p>Construye un robot</p>',
      location: 'Aula 1',
      modality: 'Presencial',
      start: new Date('2099-06-10T09:00:00Z'),
      end: new Date('2099-06-10T11:00:00Z'),
      started: false,
      highDemandRoleIds: ['role-participant'],
      assignment: { status: 'Confirmada', roleName: 'Mentor' },
      household: [
        { userId: 'child-1', name: 'Byron King', roleName: 'Participante', status: 'Solicitada' },
      ],
    })
    expect(second).toMatchObject({ id: 'activity-2', assignment: null, household: [] })
  })

  it('hides the high-demand roles until the own signups are known', () => {
    const [activity] = toTimelineActivities([eventActivity()], [], [], false, NOW)

    expect(activity?.highDemandRoleIds).toEqual([])
  })

  it('marks the activities that begin at or before now as started', () => {
    const now = new Date('2099-06-10T10:00:00Z')

    const activities = toTimelineActivities(
      [
        eventActivity({ id: 'before', startsAt: '2099-06-10T09:00:00Z' }),
        eventActivity({ id: 'now', startsAt: '2099-06-10T10:00:00Z' }),
        eventActivity({ id: 'after', startsAt: '2099-06-10T10:00:01Z' }),
      ],
      [],
      [],
      true,
      now,
    )

    expect(activities.map((activity) => [activity.id, activity.started])).toEqual([
      ['before', true],
      ['now', true],
      ['after', false],
    ])
  })
})

describe('toTimeline', () => {
  it('groups overlapping activities under the earliest start', () => {
    const clusters = toTimeline([
      timelineActivity('a', '2099-06-10T09:00:00Z', '2099-06-10T11:00:00Z'),
      timelineActivity('b', '2099-06-10T10:00:00Z', '2099-06-10T10:30:00Z'),
      timelineActivity('c', '2099-06-10T10:45:00Z', '2099-06-10T12:00:00Z'),
      timelineActivity('d', '2099-06-10T12:00:00Z', '2099-06-10T13:00:00Z'),
    ])

    expect(clusters.map((cluster) => cluster.items.map((item) => item.id))).toEqual([
      ['a', 'b', 'c'],
      ['d'],
    ])
    expect(clusters.map((cluster) => cluster.start)).toEqual([
      new Date('2099-06-10T09:00:00Z'),
      new Date('2099-06-10T12:00:00Z'),
    ])
  })

  it('has no clusters without activities', () => {
    expect(toTimeline([])).toEqual([])
  })
})
