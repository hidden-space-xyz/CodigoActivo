import { describe, expect, it } from 'vitest'

import { activityQueryKeys } from '@/entities/activity'

describe('activityQueryKeys', () => {
  it('nests every activity key under the shared root and scopes per-event keys by id', () => {
    expect(activityQueryKeys.all).toEqual(['activities'])
    expect(activityQueryKeys.adminTable()).toEqual(['activities', 'admin-table'])
    expect(activityQueryKeys.eventOptions('e1')).toEqual(['activities', 'event-options', 'e1'])
    expect(activityQueryKeys.publicByEvent('e1')).toEqual(['activities', 'public-by-event', 'e1'])
    expect(activityQueryKeys.myAssignments('e1')).toEqual(['activities', 'my-assignments', 'e1'])
    expect(activityQueryKeys.householdMembers()).toEqual(['activities', 'household-members'])
    expect(activityQueryKeys.signupRoles()).toEqual(['activities', 'signup-roles'])
    expect(activityQueryKeys.householdAssignments('e1')).toEqual([
      'activities',
      'household-assignments',
      'e1',
    ])
  })
})
