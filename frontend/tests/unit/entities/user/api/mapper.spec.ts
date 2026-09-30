import { describe, expect, it } from 'vitest'

import { toUpdateUserRequest, toUser, toUserCatalogRef } from '@/entities/user/api/mapper'

import {
  buildDependentResponse,
  buildUserResponse,
  omit,
  userTypes,
} from '../../../../support/builders'

describe('toUser', () => {
  it('maps an independent account for the admin screens', () => {
    expect(toUser(buildUserResponse({ secondaryPhone: '611111111', isAdmin: true }))).toEqual({
      id: 'user-1',
      firstName: 'Ada',
      lastName: 'Lovelace',
      email: 'ada@example.test',
      phone: '600000000',
      secondaryPhone: '611111111',
      birthDate: '',
      nationalId: '12345678Z',
      promotionalConsent: false,
      gender: 'Female',
      isAdmin: true,
      isInitialAdmin: false,
      parentId: null,
      parentName: '',
      dependentCount: 0,
      status: { id: 'status-active', name: 'Active', color: '#00FF00' },
      type: { id: 'type-participant', name: 'Participant', color: '#00AA00' },
    })
  })

  it('reads the missing contact details of a dependent as empty text', () => {
    expect(toUser(buildDependentResponse())).toMatchObject({
      email: '',
      phone: '',
      nationalId: '',
      birthDate: '2015-03-02',
      parentId: 'user-1',
      parentName: 'Ada Lovelace',
      dependentCount: 0,
    })
  })

  it('reads a user without a type as having none', () => {
    expect(toUser(omit(buildUserResponse(), 'type')).type).toBeNull()
  })
})

describe('toUserCatalogRef', () => {
  it('keeps what identifies and colors a catalog entry', () => {
    expect(userTypes.map(toUserCatalogRef)).toEqual([
      { id: 'type-participant', name: 'Participant', color: '#00AA00' },
      { id: 'type-member', name: 'Member', color: '#0000AA' },
    ])
  })
})

describe('toUpdateUserRequest', () => {
  it('sends every field of the update', () => {
    const input = {
      firstName: 'Ada',
      lastName: 'King',
      email: null,
      phone: null,
      secondaryPhone: null,
      birthDate: '2015-03-02',
      nationalId: null,
      promotionalConsent: false,
      gender: 'Female',
      parentId: 'user-1',
      currentPassword: null,
    } as const

    expect(toUpdateUserRequest(input)).toEqual(input)
  })
})
