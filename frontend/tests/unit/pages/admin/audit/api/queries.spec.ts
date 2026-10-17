import { describe, expect, it } from 'vitest'

import { auditKeys, auditList } from '@/pages/admin/audit/api/queries'

import { http, HttpResponse, paged, server } from '../../../../../support/server'
import { buildAuditEntry } from '../../../../../support/builders'
import { queryOf } from '../../../../../support/dom'

describe('auditKeys', () => {
  it('nests the list under the audit log', () => {
    expect(auditKeys.all).toEqual(['logs', 'audit'])
    expect(auditKeys.list()).toEqual(['logs', 'audit', 'list'])
  })
})

describe('auditList', () => {
  it('sends the filters, sort and page as query and maps the rows', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/logs/audit', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildAuditEntry()], 12))
      }),
    )

    const page = await auditList.fetchPage({
      search: 'marta',
      actorType: 'User',
      actorId: 'admin-1',
      action: 'SetAdmin',
      entityType: 'User',
      entityId: 'user-2',
      outcome: 'Rejected',
      clientIp: '203.0.113.9',
      traceId: 'abc',
      occurredFrom: '2026-07-01',
      occurredTo: '2026-07-04',
      page: 1,
      pageSize: 50,
      sort: 'action',
    })

    expect(page.total).toBe(12)
    expect(page.items[0]?.entity?.name).toBe('Marta Miembro')
    expect(queryOf(urls[0] ?? '')).toEqual({
      search: 'marta',
      actorType: 'User',
      actorId: 'admin-1',
      action: 'SetAdmin',
      entityType: 'User',
      entityId: 'user-2',
      outcome: 'Rejected',
      clientIp: '203.0.113.9',
      traceId: 'abc',
      occurredFrom: '2026-07-01',
      occurredTo: '2026-07-04',
      page: '1',
      pageSize: '50',
      sort: 'action',
    })
  })
})
