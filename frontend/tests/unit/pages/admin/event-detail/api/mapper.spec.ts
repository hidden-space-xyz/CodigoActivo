import { describe, expect, it } from 'vitest'

import {
  toEventAttendee,
  toEventRating,
  toEventStats,
  toEventTermsDecision,
} from '@/pages/admin/event-detail/api/mapper'

import {
  buildAttendee,
  buildAttendeeAssignment,
  buildRating,
  buildSummary,
  buildTermsDecision,
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
      scoredRatingsCount: 2,
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

  it('maps a rating without a score as unscored', () => {
    expect(toEventRating(buildRating({ score: null })).score).toBeNull()
  })

  it('maps a signed terms decision pointing at its signature image', () => {
    expect(toEventTermsDecision('e1', buildTermsDecision())).toEqual({
      userId: 'user-1',
      firstName: 'Ada',
      lastName: 'Lovelace',
      email: 'ada@example.com',
      decidedById: 'user-1',
      decidedByName: 'Ada Lovelace',
      termsDocumentId: 'terms-1',
      documentName: 'Autorización de imagen',
      documentVersion: 1,
      consent: true,
      accepted: true,
      withdrawn: false,
      decidedAt: '2026-09-01T10:00:00Z',
      signatureUrl: '/api/events/e1/terms/decisions/user-1/terms-1/signature',
    })
  })

  it('tells a withdrawn consent and a decision about conditions', () => {
    expect(
      toEventTermsDecision('e1', buildTermsDecision({ accepted: false, withdrawn: true })),
    ).toMatchObject({ consent: true, accepted: false, withdrawn: true })
    expect(
      toEventTermsDecision('e1', buildTermsDecision({ documentKind: 'Conditions' })),
    ).toMatchObject({ consent: false })
  })

  it('maps a decision without signature or email to empty strings', () => {
    expect(
      toEventTermsDecision('e1', buildTermsDecision({ email: null, hasSignature: false })),
    ).toMatchObject({ email: '', signatureUrl: '' })
  })
})
