import { describe, expect, it } from 'vitest'

import {
  isOutsideEvent,
  readActivityDraft,
  toActivityDraft,
  toDesiredCounts,
  type ActivityDraft,
  type EventDays,
} from '@/pages/admin/event-detail/model/activity-form'

import { buildActivityDetail } from '../../../../../support/models'

const ROLES = [
  { id: 'role-1', name: 'Volunteer' },
  { id: 'role-2', name: 'Mentor' },
]

const EVENT: EventDays = { startsAt: '2026-10-10', endsAt: '2026-10-12' }

function validDraft(overrides: Partial<ActivityDraft> = {}): ActivityDraft {
  return {
    title: ' Robotics ',
    description: ' Build robots ',
    location: ' Room 1 ',
    modalityId: 'mod-1',
    startsAt: new Date(2026, 9, 10, 9, 0),
    endsAt: new Date(2026, 9, 10, 11, 0),
    desiredCounts: { 'role-1': 5, 'role-2': undefined },
    ...overrides,
  }
}

describe('toActivityDraft', () => {
  it('starts blank with no target for any role', () => {
    expect(toActivityDraft(null, ROLES)).toEqual({
      title: '',
      description: '',
      location: '',
      modalityId: '',
      startsAt: null,
      endsAt: null,
      desiredCounts: { 'role-1': undefined, 'role-2': undefined },
    })
  })

  it('fills the draft from the edited activity and its saved targets', () => {
    expect(toActivityDraft(buildActivityDetail(), ROLES)).toEqual({
      title: 'Robotics',
      description: 'Build robots',
      location: 'Room 1',
      modalityId: 'mod-1',
      startsAt: new Date('2026-10-03T09:00:00.000Z'),
      endsAt: new Date('2026-10-03T11:00:00.000Z'),
      desiredCounts: { 'role-1': 4, 'role-2': undefined },
    })
  })

  it('offers a target only for the roles of the catalog', () => {
    expect(toDesiredCounts(buildActivityDetail(), [])).toEqual({})
  })
})

describe('isOutsideEvent', () => {
  it('refuses the days before and after the event, whatever the time', () => {
    expect(isOutsideEvent(new Date(2026, 9, 9, 23, 0), EVENT)).toBe(true)
    expect(isOutsideEvent(new Date(2026, 9, 10, 0, 0), EVENT)).toBe(false)
    expect(isOutsideEvent(new Date(2026, 9, 12, 23, 0), EVENT)).toBe(false)
    expect(isOutsideEvent(new Date(2026, 9, 13, 0, 0), EVENT)).toBe(true)
  })

  it('allows any day while the event days are unknown', () => {
    expect(isOutsideEvent(new Date(2000, 0, 1), { startsAt: null, endsAt: null })).toBe(false)
  })
})

describe('readActivityDraft', () => {
  it('reads a valid draft keeping only positive targets', () => {
    expect(readActivityDraft(validDraft(), EVENT)).toEqual({
      problems: {},
      value: {
        title: 'Robotics',
        description: 'Build robots',
        location: 'Room 1',
        modalityId: 'mod-1',
        startsAt: new Date(2026, 9, 10, 9, 0).toISOString(),
        endsAt: new Date(2026, 9, 10, 11, 0).toISOString(),
        roleCapacities: [{ roleTypeId: 'role-1', desiredCount: 5 }],
      },
    })
  })

  it('refuses every missing field', () => {
    expect(readActivityDraft(toActivityDraft(null, ROLES), EVENT)).toEqual({
      problems: {
        title: true,
        description: true,
        location: 'pages.admin.eventDetail.activities.form.problems.locationRequired',
        modalityId: 'pages.admin.eventDetail.activities.form.problems.modalityRequired',
        startsAt: 'pages.admin.eventDetail.activities.form.problems.startRequired',
        endsAt: 'pages.admin.eventDetail.activities.form.problems.endRequired',
      },
      value: null,
    })
  })

  it('refuses an end that is not after the start', () => {
    const start = new Date(2026, 9, 10, 9, 0)

    expect(readActivityDraft(validDraft({ startsAt: start, endsAt: start }), EVENT)).toEqual({
      problems: { endsAt: 'pages.admin.eventDetail.activities.form.problems.orderInvalid' },
      value: null,
    })
  })

  it('refuses a schedule outside the event days, marking both dates', () => {
    expect(
      readActivityDraft(
        validDraft({
          startsAt: new Date(2026, 9, 12, 22, 0),
          endsAt: new Date(2026, 9, 13, 1, 0),
        }),
        EVENT,
      ),
    ).toEqual({
      problems: {
        schedule: 'pages.admin.eventDetail.activities.form.problems.outsideEvent',
        startsAt: true,
        endsAt: true,
      },
      value: null,
    })
  })
})
