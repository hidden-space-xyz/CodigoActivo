import { describe, expect, it } from 'vitest'

import { toActivityDetail } from '@/entities/activity'
import {
  toActivityAssignment,
  toEventActivity,
  toHouseholdActivityAssignment,
  toHouseholdMember,
  toHouseholdSignupRoles,
  toOverlapCheck,
} from '@/entities/activity/api/mapper'
import type { ActivityResponse } from '@/shared/api/generated/models'

import { buildUserResponse } from '../../../../support/fixtures/user'
import { t } from '../../../../support/render'

const activity: ActivityResponse = {
  id: 'activity-1',
  title: 'Robótica',
  description: '<p>Construye un robot</p>',
  location: 'Aula 1',
  modalityId: 'modality-1',
  modalityName: 'Presencial',
  activityStartsAt: '2026-10-01T09:00:00Z',
  activityEndsAt: '2026-10-01T11:00:00Z',
  thumbnailId: 'thumb-1',
  roleCapacities: [
    { activityRoleTypeId: 'role-participant', desiredCount: 20, isHighDemand: true },
    { activityRoleTypeId: 'role-mentor', isHighDemand: false },
    { desiredCount: 3, isHighDemand: true },
  ],
}

describe('activity mapper', () => {
  it('maps a timeline activity keeping only high-demand role ids', () => {
    expect(toEventActivity(activity)).toEqual({
      id: 'activity-1',
      title: 'Robótica',
      description: '<p>Construye un robot</p>',
      location: 'Aula 1',
      modality: 'Presencial',
      startsAt: '2026-10-01T09:00:00Z',
      endsAt: '2026-10-01T11:00:00Z',
      highDemandRoleIds: ['role-participant'],
    })
  })

  it('uses the translated fallback title and empty defaults for a bare timeline activity', () => {
    expect(toEventActivity({})).toEqual({
      id: '',
      title: t('entities.activity.fallback.title'),
      description: '',
      location: '',
      modality: '',
      startsAt: null,
      endsAt: null,
      highDemandRoleIds: [],
    })
  })

  it('maps the admin detail dropping capacities without a role id', () => {
    expect(toActivityDetail(activity)).toEqual({
      id: 'activity-1',
      title: 'Robótica',
      description: '<p>Construye un robot</p>',
      location: 'Aula 1',
      modalityId: 'modality-1',
      startsAt: '2026-10-01T09:00:00Z',
      endsAt: '2026-10-01T11:00:00Z',
      thumbnailId: 'thumb-1',
      roleCapacities: [
        { roleTypeId: 'role-participant', desiredCount: 20 },
        { roleTypeId: 'role-mentor', desiredCount: null },
      ],
    })
    expect(toActivityDetail({})).toEqual({
      id: '',
      title: '',
      description: '',
      location: '',
      modalityId: '',
      startsAt: null,
      endsAt: null,
      thumbnailId: '',
      roleCapacities: [],
    })
  })

  it('maps signup roles skipping roles without id and translating missing names', () => {
    expect(
      toHouseholdSignupRoles({
        userId: 'user-1',
        roles: [{ id: 'role-1', name: 'Participante' }, { id: 'role-2' }, { name: 'Sin id' }],
      }),
    ).toEqual({
      userId: 'user-1',
      roles: [
        { id: 'role-1', name: 'Participante' },
        { id: 'role-2', name: t('entities.activity.fallback.role') },
      ],
    })
    expect(toHouseholdSignupRoles({})).toEqual({ userId: '', roles: [] })
  })

  it('maps an own assignment and shows a dash for a missing status', () => {
    expect(
      toActivityAssignment({
        activityId: 'activity-1',
        status: { name: 'Confirmada' },
        roleType: { name: 'Mentor' },
      }),
    ).toEqual({ activityId: 'activity-1', status: 'Confirmada', roleName: 'Mentor' })
    expect(toActivityAssignment({ status: { name: '' } })).toEqual({
      activityId: '',
      status: '—',
      roleName: '',
    })
    expect(toActivityAssignment({})).toEqual({ activityId: '', status: '—', roleName: '' })
  })

  it('maps a household assignment joining the member name', () => {
    expect(
      toHouseholdActivityAssignment({
        activityId: 'activity-1',
        userId: 'child-1',
        firstName: 'Byron',
        lastName: 'King',
        roleName: 'Participante',
        statusName: 'Solicitada',
      }),
    ).toEqual({
      activityId: 'activity-1',
      userId: 'child-1',
      name: 'Byron King',
      roleName: 'Participante',
      status: 'Solicitada',
    })
    expect(toHouseholdActivityAssignment({ lastName: 'King' })).toEqual({
      activityId: '',
      userId: '',
      name: 'King',
      roleName: '',
      status: '',
    })
    expect(toHouseholdActivityAssignment({ firstName: 'Byron' }).name).toBe('Byron')
  })

  it('reduces a minor to id and full name', () => {
    expect(toHouseholdMember(buildUserResponse({ id: 'child-1', firstName: 'Byron' }))).toEqual({
      id: 'child-1',
      name: 'Byron Lovelace',
    })
    expect(toHouseholdMember({})).toEqual({ id: '', name: '' })
  })

  it('maps the overlap check and defaults missing values', () => {
    expect(
      toOverlapCheck({
        hasOverlaps: true,
        overlaps: [
          {
            activityId: 'activity-2',
            title: 'Python',
            startsAt: '2026-10-01T10:00:00Z',
            endsAt: '2026-10-01T12:00:00Z',
          },
          {},
        ],
      }),
    ).toEqual({
      hasOverlaps: true,
      overlaps: [
        {
          activityId: 'activity-2',
          title: 'Python',
          startsAt: '2026-10-01T10:00:00Z',
          endsAt: '2026-10-01T12:00:00Z',
        },
        { activityId: '', title: '', startsAt: null, endsAt: null },
      ],
    })
    expect(toOverlapCheck({})).toEqual({ hasOverlaps: false, overlaps: [] })
  })
})
