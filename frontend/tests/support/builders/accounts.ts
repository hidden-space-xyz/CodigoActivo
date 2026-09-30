import type {
  EventCertificateResponse,
  EventHistoryActivityResponse,
  EventHistoryResponse,
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
