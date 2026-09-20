import type { AccountCertificate } from '@/entities/account'
import type {
  EventCertificateResponse,
  EventHistoryActivityResponse,
  EventHistoryResponse,
  UserResponse,
} from '@/shared/api/generated/models'

/** Mapped certificate as the account feature consumes it. */
export function buildCertificate(overrides: Partial<AccountCertificate> = {}): AccountCertificate {
  return {
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
    ...overrides,
  }
}

/** Wire certificate as returned by `/api/me/certificates`. */
export function buildCertificateResponse(
  overrides: EventCertificateResponse = {},
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
  overrides: EventHistoryActivityResponse = {},
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
export function buildHistoryResponse(overrides: EventHistoryResponse = {}): EventHistoryResponse {
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

/** Wire minor as returned by the users endpoints. */
export function buildChildResponse(overrides: UserResponse = {}): UserResponse {
  return {
    id: 'child-1',
    firstName: 'Byron',
    lastName: 'Lovelace',
    email: null,
    phone: null,
    birthDate: '2015-03-02',
    gender: 'Male',
    parentId: 'user-1',
    parentName: 'Ada Lovelace',
    isAdmin: false,
    ...overrides,
  }
}

/** Shallow copy of an object without the given keys, for fixtures lacking optional wire fields. */
export function omit<T extends object, K extends keyof T>(value: T, ...keys: K[]): Omit<T, K> {
  const copy = { ...value }
  for (const key of keys) delete copy[key]
  return copy
}
