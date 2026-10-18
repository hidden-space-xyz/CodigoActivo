import type {
  AuditLogEntryResponse,
  DiagnosticLogEntryDetailResponse,
  DiagnosticLogEntryResponse,
} from '@/shared/api/generated/models'

/** Trace id shared by the log builders. */
export const TRACE_ID = '4bf92f3577b34da6a3ce929d0e0e4736'

/** Diagnostic list row as returned by `GET /api/logs/diagnostics`. */
export function buildDiagnosticEntry(
  overrides: Partial<DiagnosticLogEntryResponse> = {},
): DiagnosticLogEntryResponse {
  return {
    id: '0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b',
    occurredAt: '2026-07-04T09:02:05Z',
    level: 'Error',
    category: 'CodigoActivo.API.Middlewares.GlobalExceptionHandler',
    eventName: 'UnhandledRequestException',
    message: 'Unhandled exception while processing GET api/users/{id}',
    hasException: true,
    traceId: TRACE_ID,
    ...overrides,
  }
}

/** Diagnostic entry as returned by `GET /api/logs/diagnostics/:id`. */
export function buildDiagnosticDetail(
  overrides: Partial<DiagnosticLogEntryDetailResponse> = {},
): DiagnosticLogEntryDetailResponse {
  return {
    id: '0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b',
    occurredAt: '2026-07-04T09:02:05Z',
    level: 'Error',
    category: 'CodigoActivo.API.Middlewares.GlobalExceptionHandler',
    eventName: 'UnhandledRequestException',
    message: 'Unhandled exception while processing GET api/users/{id}',
    exception: 'System.InvalidOperationException: boom\n   at Somewhere()',
    properties: '{"Method": "GET", "RouteTemplate": "api/users/{id}"}',
    traceId: TRACE_ID,
    ...overrides,
  }
}

/** Audit list row as returned by `GET /api/logs/audit`. */
export function buildAuditEntry(
  overrides: Partial<AuditLogEntryResponse> = {},
): AuditLogEntryResponse {
  return {
    id: '0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a6c',
    occurredAt: '2026-07-04T09:01:00Z',
    actorType: 'User',
    actorId: 'admin-1',
    actorName: 'Ada Admin',
    actorEmail: 'admin@example.com',
    clientIp: '203.0.113.9',
    action: 'SetAdmin',
    entityType: 'User',
    entityId: 'user-2',
    entityName: 'Marta Miembro',
    related: [
      { type: 'Activity', id: 'aaaaaaaa-1111-1111-1111-111111111111', name: 'Taller de robótica' },
    ],
    outcome: 'Succeeded',
    errorCode: null,
    traceId: TRACE_ID,
    ...overrides,
  }
}
