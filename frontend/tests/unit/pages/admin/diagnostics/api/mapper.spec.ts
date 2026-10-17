import { describe, expect, it } from 'vitest'

import { toDiagnosticDetail, toDiagnosticEntry } from '@/pages/admin/diagnostics/api/mapper'

import {
  buildDiagnosticDetail,
  buildDiagnosticEntry,
  TRACE_ID,
} from '../../../../../support/builders'

describe('toDiagnosticEntry', () => {
  it('keeps the row and turns missing values into empty text', () => {
    expect(toDiagnosticEntry(buildDiagnosticEntry())).toEqual({
      id: '0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b',
      occurredAt: '2026-07-04T09:02:05Z',
      level: 'Error',
      category: 'CodigoActivo.API.Middlewares.GlobalExceptionHandler',
      eventName: 'UnhandledRequestException',
      message: 'Unhandled exception while processing GET api/users/{id}',
      hasException: true,
      traceId: TRACE_ID,
    })
    expect(
      toDiagnosticEntry(buildDiagnosticEntry({ eventName: null, traceId: null })),
    ).toMatchObject({ eventName: '', traceId: '' })
  })
})

describe('toDiagnosticDetail', () => {
  it('indents the template values and keeps the exception', () => {
    const detail = toDiagnosticDetail(buildDiagnosticDetail())

    expect(detail.properties).toBe('{\n  "Method": "GET",\n  "RouteTemplate": "api/users/{id}"\n}')
    expect(detail.exception).toBe('System.InvalidOperationException: boom\n   at Somewhere()')
  })

  it('keeps values that are not JSON as they came and empties the missing ones', () => {
    expect(toDiagnosticDetail(buildDiagnosticDetail({ properties: 'not json' })).properties).toBe(
      'not json',
    )
    expect(
      toDiagnosticDetail(
        buildDiagnosticDetail({
          properties: null,
          exception: null,
          eventName: null,
          traceId: null,
        }),
      ),
    ).toMatchObject({ properties: '', exception: '', eventName: '', traceId: '' })
  })
})
