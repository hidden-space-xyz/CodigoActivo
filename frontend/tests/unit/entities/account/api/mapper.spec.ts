import { describe, expect, it } from 'vitest'

import {
  toAccountCertificate,
  toAccountChild,
  toAccountHistoryEntry,
  toAccountProfile,
  toAddMinorRequest,
  toAuthenticatorSetup,
  toSaveEventRatingRequest,
  toUpdateMinorRequest,
  toUpdateProfileRequest,
} from '@/entities/account/api/mapper'

import { buildUserResponse } from '../../../../support/fixtures/user'

describe('account mapper', () => {
  it('maps a full user response to the account profile', () => {
    expect(toAccountProfile(buildUserResponse({ isAdmin: true }))).toEqual({
      id: 'user-1',
      firstName: 'Ada',
      lastName: 'Lovelace',
      email: 'ada@example.test',
      phone: '600000000',
      birthDate: '1990-05-10',
      gender: 'Female',
      statusName: 'Active',
      isAdmin: true,
      twoFactorMethod: 'Email',
    })
  })

  it('defaults missing profile fields to empty values', () => {
    expect(toAccountProfile({})).toEqual({
      id: '',
      firstName: '',
      lastName: '',
      email: '',
      phone: '',
      birthDate: '',
      gender: null,
      statusName: '',
      isAdmin: false,
      twoFactorMethod: 'Email',
    })
  })

  it('keeps the authenticator method of the profile', () => {
    expect(
      toAccountProfile(buildUserResponse({ twoFactorMethod: 'Authenticator' })).twoFactorMethod,
    ).toBe('Authenticator')
  })

  it('maps the authenticator enrollment data, defaulting missing text to empty', () => {
    expect(
      toAuthenticatorSetup({ sharedKey: 'ABCD EFGH', authenticatorUri: 'otpauth://totp/x' }),
    ).toEqual({ sharedKey: 'ABCD EFGH', authenticatorUri: 'otpauth://totp/x' })
    expect(toAuthenticatorSetup({})).toEqual({ sharedKey: '', authenticatorUri: '' })
  })

  it('maps a minor to the reduced child shape', () => {
    expect(toAccountChild(buildUserResponse({ id: 'child-1', gender: 'Male' }))).toEqual({
      id: 'child-1',
      firstName: 'Ada',
      lastName: 'Lovelace',
      birthDate: '1990-05-10',
      gender: 'Male',
    })
    expect(toAccountChild({})).toEqual({
      id: '',
      firstName: '',
      lastName: '',
      birthDate: '',
      gender: null,
    })
  })

  it('builds the profile update body with a null parent', () => {
    expect(
      toUpdateProfileRequest({
        firstName: 'Ada',
        lastName: 'King',
        email: 'ada@example.test',
        phone: '611111111',
        birthDate: '1990-05-10',
        gender: 'Female',
        currentPassword: 'Str0ngPass!23',
      }),
    ).toEqual({
      firstName: 'Ada',
      lastName: 'King',
      email: 'ada@example.test',
      phone: '611111111',
      birthDate: '1990-05-10',
      gender: 'Female',
      parentId: null,
      currentPassword: 'Str0ngPass!23',
    })
  })

  it('builds the minor bodies without contact data and keeps the parent link on update', () => {
    const input = {
      firstName: 'Byron',
      lastName: 'King',
      birthDate: '2015-01-02',
      gender: 'Other',
    } as const

    expect(toAddMinorRequest(input)).toEqual(input)
    expect(toUpdateMinorRequest(input, 'parent-1')).toEqual({ ...input, parentId: 'parent-1' })
  })

  it('maps a history entry with its rateable flag and activities', () => {
    const entry = toAccountHistoryEntry({
      eventId: 'event-1',
      title: 'Día Código Activo',
      subtitle: 'Edición 2025',
      eventStartsAt: '2025-05-10T09:00:00Z',
      eventEndsAt: '2025-05-10T18:00:00Z',
      thumbnailId: 'thumb-1',
      isPast: true,
      canRate: true,
      activities: [
        {
          activityId: 'activity-1',
          title: 'Robótica',
          location: 'Aula 1',
          modalityName: 'Presencial',
          userId: 'child-1',
          firstName: 'Byron',
          lastName: 'King',
          isSelf: false,
          roleTypeName: 'Participante',
          statusName: 'Confirmada',
        },
        { firstName: 'Solo' },
        { lastName: 'Apellido' },
      ],
    })

    expect(entry).toEqual({
      eventId: 'event-1',
      title: 'Día Código Activo',
      subtitle: 'Edición 2025',
      startsAt: '2025-05-10T09:00:00Z',
      endsAt: '2025-05-10T18:00:00Z',
      thumbnailId: 'thumb-1',
      isPast: true,
      canRate: true,
      activities: [
        {
          activityId: 'activity-1',
          title: 'Robótica',
          location: 'Aula 1',
          modality: 'Presencial',
          participantId: 'child-1',
          participantName: 'Byron King',
          isSelf: false,
          roleName: 'Participante',
          statusName: 'Confirmada',
        },
        {
          activityId: '',
          title: '',
          location: '',
          modality: '',
          participantId: '',
          participantName: 'Solo',
          isSelf: false,
          roleName: '',
          statusName: '',
        },
        expect.objectContaining({ participantName: 'Apellido' }),
      ],
    })
  })

  it('maps an empty history entry as not rateable with no activities', () => {
    expect(toAccountHistoryEntry({})).toEqual({
      eventId: '',
      title: '',
      subtitle: '',
      startsAt: '',
      endsAt: '',
      thumbnailId: '',
      isPast: false,
      canRate: false,
      activities: [],
    })
  })

  it('maps certificates and defaults missing fields', () => {
    expect(
      toAccountCertificate({
        code: 'CA-001',
        eventId: 'event-1',
        userId: 'user-1',
        firstName: 'Ada',
        lastName: 'Lovelace',
        isSelf: true,
        eventTitle: 'Día',
        eventSubtitle: 'Sub',
        eventStartsAt: '2025-05-10T09:00:00Z',
        eventEndsAt: '2025-05-10T18:00:00Z',
      }),
    ).toEqual({
      code: 'CA-001',
      eventId: 'event-1',
      participantId: 'user-1',
      firstName: 'Ada',
      lastName: 'Lovelace',
      isSelf: true,
      eventTitle: 'Día',
      eventSubtitle: 'Sub',
      startsAt: '2025-05-10T09:00:00Z',
      endsAt: '2025-05-10T18:00:00Z',
    })
    expect(toAccountCertificate({})).toEqual({
      code: '',
      eventId: '',
      participantId: '',
      firstName: '',
      lastName: '',
      isSelf: false,
      eventTitle: '',
      eventSubtitle: '',
      startsAt: '',
      endsAt: '',
    })
  })

  it('trims rating comments and sends blank ones as null', () => {
    expect(
      toSaveEventRatingRequest({
        score: 3,
        mostLiked: '  Los talleres  ',
        leastLiked: '   ',
        suggestions: '',
      }),
    ).toEqual({ score: 3, mostLiked: 'Los talleres', leastLiked: null, suggestions: null })
    expect(
      toSaveEventRatingRequest({
        score: 0,
        mostLiked: ' ',
        leastLiked: 'La espera ',
        suggestions: ' Más talleres',
      }),
    ).toEqual({ score: 0, mostLiked: null, leastLiked: 'La espera', suggestions: 'Más talleres' })
  })
})
