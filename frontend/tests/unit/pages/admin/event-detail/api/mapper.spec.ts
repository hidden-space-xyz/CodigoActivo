import { describe, expect, it } from 'vitest'

import { toEventAttendee, toEventRating, toEventStats } from '@/pages/admin/event-detail/api/mapper'

import {
  buildAttendee,
  buildAttendeeAssignment,
  buildRating,
  buildSummary,
} from '../../../../../support/builders'

describe('event detail mapper', () => {
  it('maps the counts of the summary cards', () => {
    expect(
      toEventStats(
        buildSummary({
          roleTypeBreakdown: [
            { roleTypeId: 'role-1', roleTypeName: 'Volunteer', approvedAssignments: 7 },
            { roleTypeId: 'role-2', roleTypeName: null, approvedAssignments: 1 },
          ],
        }),
      ),
    ).toEqual({
      activitiesCount: 3,
      roles: [
        { name: 'Volunteer', approved: 7 },
        { name: '', approved: 1 },
      ],
      ratingsCount: 2,
      ratingsAverage: 4.25,
    })
    expect(toEventStats(buildSummary({ ratingsAverage: null })).ratingsAverage).toBeNull()
  })

  it('maps an adult attendee with their signups', () => {
    expect(toEventAttendee(buildAttendee())).toEqual({
      userId: 'user-1',
      firstName: 'Ada',
      lastName: 'Lovelace',
      email: 'ada@example.test',
      phone: '600000001',
      secondaryPhone: '',
      birthDate: '1990-01-15',
      gender: 'Female',
      userTypeName: 'Member',
      userTypeColor: '#ff0000',
      guardian: null,
      assignments: [
        {
          activityId: 'act-1',
          activityTitle: 'Robotics',
          roleTypeId: 'role-1',
          roleName: 'Volunteer',
          statusId: 'status-1',
          statusName: 'Requested',
          signedUpAt: '2026-09-02T10:00:00Z',
          hasTimeConflict: false,
        },
      ],
    })
  })

  it('maps a minor with a guardian and leaves unknown data empty', () => {
    const attendee = toEventAttendee(
      buildAttendee({
        firstName: null,
        lastName: null,
        email: null,
        phone: null,
        birthDate: null,
        guardian: { firstName: 'Grace', lastName: 'Hopper', email: null, phone: '600000002' },
        assignments: [buildAttendeeAssignment({ roleTypeName: null, statusName: null })],
      }),
    )

    expect(attendee).toMatchObject({
      firstName: '',
      lastName: '',
      email: '',
      phone: '',
      birthDate: null,
      guardian: {
        firstName: 'Grace',
        lastName: 'Hopper',
        email: '',
        phone: '600000002',
        secondaryPhone: '',
      },
    })
    expect(attendee.assignments[0]).toMatchObject({ roleName: '', statusName: '' })
  })

  it('maps a rating leaving unanswered questions empty', () => {
    expect(toEventRating(buildRating())).toEqual({
      id: 'rating-1',
      score: 4,
      mostLiked: 'The people',
      leastLiked: '',
      suggestions: '',
    })
  })
})
