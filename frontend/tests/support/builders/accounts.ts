import type {
  EventCertificateResponse,
  EventHistoryActivityResponse,
  EventHistoryResponse,
  PersonalDataResponse,
} from '@/shared/api/generated/models'

/** Wire certificate as returned by `/api/me/certificates`. */
export function buildCertificateResponse(
  overrides: Partial<EventCertificateResponse> = {},
): EventCertificateResponse {
  return {
    code: 'CA-2025-0001',
    eventId: 'event-1',
    userId: 'user-1',
    firstName: 'Ada',
    lastName: 'Lovelace',
    isSelf: true,
    eventTitle: 'Hackathon de Primavera',
    eventSubtitle: 'Edición 2025',
    eventStartsAt: '2025-05-10',
    eventEndsAt: '2025-05-12',
    ...overrides,
  }
}

/** Wire activity inside a history entry. */
export function buildHistoryActivityResponse(
  overrides: Partial<EventHistoryActivityResponse> = {},
): EventHistoryActivityResponse {
  return {
    activityId: 'activity-1',
    title: 'Taller de robótica',
    location: 'Aula 1',
    modalityName: 'Presencial',
    userId: 'user-1',
    firstName: 'Ada',
    lastName: 'Lovelace',
    isSelf: true,
    roleTypeId: 'role-1',
    roleTypeName: 'Participante',
    statusId: 'status-1',
    statusName: 'Confirmada',
    ...overrides,
  }
}

/** Wire history entry as returned by `/api/me/event-history`. */
export function buildHistoryResponse(
  overrides: Partial<EventHistoryResponse> = {},
): EventHistoryResponse {
  return {
    eventId: 'event-1',
    title: 'Hackathon de Primavera',
    subtitle: 'Edición 2025',
    eventStartsAt: '2025-05-10',
    eventEndsAt: '2025-05-12',
    thumbnailId: 'thumb-1',
    isPast: false,
    canRate: false,
    activities: [buildHistoryActivityResponse()],
    ...overrides,
  }
}

/** Wire copy of a household's personal data with one entry of every kind. */
export function buildPersonalDataResponse(
  overrides: Partial<PersonalDataResponse> = {},
): PersonalDataResponse {
  return {
    generatedAt: '2026-10-18T10:00:00Z',
    account: {
      id: 'user-1',
      firstName: 'Ada',
      lastName: 'Lovelace',
      email: 'ada@example.test',
      pendingEmail: 'ada@new.test',
      phone: '600000000',
      secondaryPhone: null,
      nationalId: '12345678Z',
      gender: 'Female',
      newsConsent: true,
      userType: 'Socio',
      status: 'Activa',
      isAdmin: false,
      twoFactorMethod: 'Authenticator',
      createdAt: '2026-01-01T00:00:00Z',
      updatedAt: null,
      lastLoginAt: '2026-10-18T09:00:00Z',
      adultDeclaredAt: '2026-01-01T00:00:00Z',
    },
    newsConsentChanges: [
      { granted: true, source: 'Registration', changedAt: '2026-01-01T00:00:00Z' },
      { granted: false, source: 'Imported', changedAt: '2026-02-01T00:00:00Z' },
    ],
    dependents: [
      {
        id: 'child-1',
        firstName: 'Byron',
        lastName: 'Lovelace',
        birthDate: '2015-03-02',
        gender: 'Male',
        userType: 'Participante',
        createdAt: '2026-01-01T00:00:00Z',
        updatedAt: null,
        guardianshipDeclaredAt: '2026-01-01T00:00:00Z',
        comingOfAgeNoticeSentAt: '2026-06-01T00:00:00Z',
      },
    ],
    signups: [
      {
        personId: 'child-1',
        personName: 'Byron Lovelace',
        eventTitle: 'Hackatón',
        activityTitle: 'Taller',
        activityStartsAt: '2026-06-01T10:00:00Z',
        role: 'Participante',
        status: 'Confirmada',
        signedUpAt: '2026-05-01T00:00:00Z',
      },
    ],
    termsDecisions: [
      {
        eventTitle: 'Hackatón',
        documentName: 'Uso de imágenes',
        accepted: true,
        decidedAt: '2026-05-01T00:00:00Z',
        signature: 'iVBORw0KGgo=',
      },
    ],
    sessions: [{ createdAt: '2026-10-18T09:00:00Z', expiresAt: '2026-10-18T21:00:00Z' }],
    securityLog: [
      {
        occurredAt: '2026-10-18T08:59:00Z',
        action: 'Login',
        outcome: 'Succeeded',
        actorType: 'Anonymous',
        ranBySelf: false,
        clientIp: '203.0.113.1',
      },
      {
        occurredAt: '2026-10-18T09:05:00Z',
        action: 'ChangePassword',
        outcome: 'Rejected',
        actorType: 'User',
        ranBySelf: true,
        clientIp: '203.0.113.1',
      },
      {
        occurredAt: '2026-10-18T09:10:00Z',
        action: 'ChangeUserType',
        outcome: 'Succeeded',
        actorType: 'User',
        ranBySelf: false,
        clientIp: null,
      },
    ],
    ...overrides,
  }
}
