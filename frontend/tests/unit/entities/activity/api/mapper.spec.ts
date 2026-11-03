import { describe, expect, it } from 'vitest'

import {
  toActivityDetail,
  toEventActivity,
  toHouseholdActivityAssignment,
} from '@/entities/activity/api/mapper'

import { buildActivityResponse, buildHouseholdAssignment } from '../../../../support/builders'

const activity = buildActivityResponse({
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
    { activityRoleTypeId: 'role-mentor', desiredCount: 2, isHighDemand: false },
  ],
})

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

  it('maps the admin detail with every role capacity', () => {
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
        { roleTypeId: 'role-mentor', desiredCount: 2 },
      ],
    })
  })

  it('maps a household signup joining the member name', () => {
    expect(
      toHouseholdActivityAssignment(
        buildHouseholdAssignment({ activityId: 'activity-1', statusName: 'Confirmada' }),
      ),
    ).toEqual({
      activityId: 'activity-1',
      userId: 'child-1',
      name: 'Byron King',
      roleName: 'Participante',
      status: 'Confirmada',
    })
    expect(toHouseholdActivityAssignment(buildHouseholdAssignment({ lastName: '' })).name).toBe(
      'Byron',
    )
  })
})
