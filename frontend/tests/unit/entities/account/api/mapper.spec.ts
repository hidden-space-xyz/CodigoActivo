import { describe, expect, it } from 'vitest'

import {
  toAccountCertificate,
  toAccountChild,
  toAccountHistoryEntry,
  toAccountProfile,
  toAuthenticatorSetup,
  toMinorRequest,
  toRegisterRequest,
  toRegistrationResult,
  toSaveEventRatingRequest,
  toUpdateMinorRequest,
  toUpdateProfileRequest,
} from '@/entities/account/api/mapper'

import {
  buildCertificateResponse,
  buildDependentResponse,
  buildHistoryActivityResponse,
  buildHistoryResponse,
  buildUserResponse,
} from '../../../../support/builders'

const MINOR = {
  firstName: 'Byron',
  lastName: 'Lovelace',
  birthDate: '2015-03-02',
  gender: 'Male',
} as const

describe('account mapper', () => {
  it('maps the own profile, missing contact details as empty text', () => {
    expect(toAccountProfile(buildUserResponse({ email: null, isAdmin: true }))).toEqual({
      id: 'user-1',
      firstName: 'Ada',
      lastName: 'Lovelace',
      email: '',
      phone: '600000000',
      secondaryPhone: '',
      nationalId: '12345678Z',
      promotionalConsent: false,
      gender: 'Female',
      statusName: 'Active',
      isAdmin: true,
    })
  })

  it('maps the authenticator enrollment and the minors', () => {
    expect(
      toAuthenticatorSetup({ sharedKey: 'ABCD EFGH', authenticatorUri: 'otpauth://x' }),
    ).toEqual({
      sharedKey: 'ABCD EFGH',
      authenticatorUri: 'otpauth://x',
    })
    expect(toAccountChild(buildDependentResponse())).toEqual({
      id: 'child-1',
      firstName: 'Byron',
      lastName: 'Lovelace',
      birthDate: '2015-03-02',
      gender: 'Male',
    })
  })

  it('builds the profile update of an adult without birth date nor guardian', () => {
    expect(
      toUpdateProfileRequest({
        firstName: 'Ada',
        lastName: 'King',
        email: 'ada@example.test',
        phone: '600000000',
        secondaryPhone: null,
        nationalId: '12345678Z',
        promotionalConsent: true,
        gender: 'Female',
        currentPassword: null,
      }),
    ).toMatchObject({ lastName: 'King', birthDate: null, parentId: null, currentPassword: null })
  })

  it('builds the minor bodies, keeping an edited minor under its guardian', () => {
    expect(toMinorRequest(MINOR)).toEqual(MINOR)
    expect(toUpdateMinorRequest(MINOR, 'user-1')).toEqual({
      ...MINOR,
      promotionalConsent: false,
      parentId: 'user-1',
    })
  })

  it('joins participant names in the history and keeps rating flags', () => {
    const entry = toAccountHistoryEntry(
      buildHistoryResponse({
        isPast: true,
        canRate: true,
        activities: [buildHistoryActivityResponse({ firstName: 'Byron', isSelf: false })],
      }),
    )

    expect(entry).toMatchObject({ eventId: 'event-1', isPast: true, canRate: true })
    expect(entry.activities).toEqual([
      {
        activityId: 'activity-1',
        title: 'Taller de robótica',
        location: 'Aula 1',
        modality: 'Presencial',
        participantId: 'user-1',
        participantName: 'Byron Lovelace',
        isSelf: false,
        roleName: 'Participante',
        statusName: 'Confirmada',
      },
    ])
  })

  it('maps a certificate', () => {
    expect(toAccountCertificate(buildCertificateResponse())).toEqual({
      code: 'CA-2025-0001',
      eventId: 'event-1',
      participantId: 'user-1',
      firstName: 'Ada',
      lastName: 'Lovelace',
      isSelf: true,
      eventTitle: 'Hackathon de Primavera',
      eventSubtitle: 'Edición 2025',
      startsAt: '2025-05-10',
      endsAt: '2025-05-12',
    })
  })

  it('trims rating comments and sends blank ones as none', () => {
    expect(
      toSaveEventRatingRequest({
        score: 4,
        mostLiked: ' Todo ',
        leastLiked: '  ',
        suggestions: '',
      }),
    ).toEqual({ score: 4, mostLiked: 'Todo', leastLiked: null, suggestions: null })
  })

  it('builds the registration of an adult with minors and reads its result', () => {
    const adult = {
      firstName: 'Ada',
      lastName: 'Lovelace',
      gender: 'Female',
      email: 'ada@example.test',
      phone: '600000000',
      secondaryPhone: null,
      nationalId: '12345678Z',
      promotionalConsent: false,
    } as const

    expect(toRegisterRequest({ adult, password: 'Str0ngPass!23', minors: [MINOR] })).toEqual({
      ...adult,
      password: 'Str0ngPass!23',
      minors: [MINOR],
    })
    expect(
      toRegistrationResult({ adult: buildUserResponse(), minors: [buildDependentResponse()] }),
    ).toEqual({ adultId: 'user-1', minorCount: 1 })
  })
})
