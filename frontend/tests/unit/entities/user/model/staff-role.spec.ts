import { describe, expect, it } from 'vitest'

import { STAFF_ROLES, staffRoleLabelKey } from '@/entities/user'

import { t } from '../../../../support/render'

describe('staff role', () => {
  it('names the label of each cargo', () => {
    expect(STAFF_ROLES.map((staffRole) => t(staffRoleLabelKey(staffRole)))).toEqual([
      'Sin cargo',
      'Editor',
      'Administrador',
    ])
  })
})
