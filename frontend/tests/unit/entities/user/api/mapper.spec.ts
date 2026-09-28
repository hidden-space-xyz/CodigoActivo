import { describe, expect, it } from 'vitest'

import { toUser } from '@/entities/user/api/mapper'

import { buildUserResponse } from '../../../../support/fixtures/user'

describe('user mapper', () => {
  it('maps a user for the admin screens with status and type references', () => {
    expect(
      toUser(
        buildUserResponse({
          parentId: 'parent-1',
          parentName: 'Ada Lovelace',
          dependentCount: 2,
          isAdmin: true,
          isInitialAdmin: true,
          status: { id: 'status-active', name: 'Active', color: '#00ff00' },
        }),
      ),
    ).toEqual({
      id: 'user-1',
      firstName: 'Ada',
      lastName: 'Lovelace',
      email: 'ada@example.test',
      phone: '600000000',
      secondaryPhone: '',
      birthDate: '',
      nationalId: '12345678Z',
      promotionalConsent: false,
      gender: 'Female',
      isAdmin: true,
      isInitialAdmin: true,
      parentId: 'parent-1',
      parentName: 'Ada Lovelace',
      dependentCount: 2,
      status: { id: 'status-active', name: 'Active', color: '#00ff00' },
      type: { id: 'type-participant', name: 'Participant', color: null },
      twoFactorMethod: 'Email',
    })
  })

  it('defaults missing fields and turns missing references into null', () => {
    expect(toUser({})).toEqual({
      id: '',
      firstName: '',
      lastName: '',
      email: '',
      phone: '',
      secondaryPhone: '',
      birthDate: '',
      nationalId: '',
      promotionalConsent: false,
      gender: null,
      isAdmin: false,
      isInitialAdmin: false,
      parentId: null,
      parentName: '',
      dependentCount: 0,
      status: null,
      type: null,
      twoFactorMethod: 'Email',
    })
    expect(toUser({ status: {}, type: {} })).toMatchObject({
      status: { id: '', name: '', color: null },
      type: { id: '', name: '', color: null },
    })
  })
})
