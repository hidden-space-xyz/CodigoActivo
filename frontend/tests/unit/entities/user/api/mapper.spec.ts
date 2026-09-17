import { describe, expect, it } from 'vitest'

import { toUpdateUserRequest, toUser } from '@/entities/user/api/mapper'

import { buildUserResponse } from '../../../../support/fixtures/user'

describe('user mapper', () => {
  it('maps a user for the admin screens with status and type references', () => {
    expect(
      toUser(
        buildUserResponse({
          parentId: 'parent-1',
          parentName: 'Ada Lovelace',
          dependentCount: 2,
          status: { id: 'status-active', name: 'Active', color: '#00ff00' },
        }),
      ),
    ).toEqual({
      id: 'user-1',
      firstName: 'Ada',
      lastName: 'Lovelace',
      email: 'ada@example.test',
      phone: '600000000',
      birthDate: '1990-05-10',
      gender: 'Female',
      isAdmin: false,
      parentId: 'parent-1',
      parentName: 'Ada Lovelace',
      dependentCount: 2,
      status: { id: 'status-active', name: 'Active', color: '#00ff00' },
      type: { id: 'type-participant', name: 'Participant', color: null },
    })
  })

  it('defaults missing fields and turns missing references into null', () => {
    expect(toUser({})).toEqual({
      id: '',
      firstName: '',
      lastName: '',
      email: '',
      phone: '',
      birthDate: '',
      gender: null,
      isAdmin: false,
      parentId: null,
      parentName: '',
      dependentCount: 0,
      status: null,
      type: null,
    })
    expect(toUser({ status: {}, type: {} })).toMatchObject({
      status: { id: '', name: '', color: null },
      type: { id: '', name: '', color: null },
    })
  })

  it('copies the form input into the update body', () => {
    const input = {
      firstName: 'Byron',
      lastName: 'King',
      email: null,
      phone: null,
      birthDate: '2015-01-02',
      gender: 'Male',
      parentId: 'parent-1',
    } as const

    expect(toUpdateUserRequest(input)).toEqual(input)
  })
})
