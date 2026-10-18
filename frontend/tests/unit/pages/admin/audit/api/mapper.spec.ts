import { describe, expect, it } from 'vitest'

import { toAuditEntry } from '@/pages/admin/audit/api/mapper'

import { buildAuditEntry, TRACE_ID } from '../../../../../support/builders'

describe('toAuditEntry', () => {
  it('keeps the entry with its main entity and related entities', () => {
    expect(toAuditEntry(buildAuditEntry())).toEqual({
      id: '0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a6c',
      occurredAt: '2026-07-04T09:01:00Z',
      actorType: 'User',
      actorId: 'admin-1',
      actorName: 'Ada Admin',
      actorEmail: 'admin@example.com',
      clientIp: '203.0.113.9',
      action: 'SetAdmin',
      entity: { type: 'User', id: 'user-2', name: 'Marta Miembro' },
      related: [{ type: 'Activity', id: 'aaaaaaaa-1111-1111-1111-111111111111' }],
      outcome: 'Succeeded',
      errorCode: '',
      traceId: TRACE_ID,
    })
  })

  it('turns missing values into empty text and no main entity', () => {
    expect(
      toAuditEntry(
        buildAuditEntry({
          actorType: 'System',
          actorId: null,
          actorName: null,
          actorEmail: null,
          clientIp: null,
          entityId: null,
          entityName: null,
          related: [],
          errorCode: 'UniqueConstraintViolation',
          traceId: null,
        }),
      ),
    ).toMatchObject({
      actorId: '',
      actorName: '',
      actorEmail: '',
      clientIp: '',
      entity: null,
      related: [],
      errorCode: 'UniqueConstraintViolation',
      traceId: '',
    })
  })

  it('keeps an entity whose user no longer exists without a name', () => {
    expect(toAuditEntry(buildAuditEntry({ entityName: null })).entity).toEqual({
      type: 'User',
      id: 'user-2',
      name: '',
    })
  })
})
