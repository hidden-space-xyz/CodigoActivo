import { describe, expect, it } from 'vitest'

import {
  diagnosticKeys,
  diagnosticList,
  diagnosticQueries,
} from '@/pages/admin/diagnostics/api/queries'

import { http, HttpResponse, paged, server } from '../../../../../support/server'
import { buildDiagnosticDetail, buildDiagnosticEntry } from '../../../../../support/builders'
import { queryOf } from '../../../../../support/dom'
import { createTestQueryClient } from '../../../../../support/render'

describe('diagnosticKeys', () => {
  it('nests the list and every entry under the diagnostic log', () => {
    expect(diagnosticKeys.all).toEqual(['logs', 'diagnostics'])
    expect(diagnosticKeys.list()).toEqual(['logs', 'diagnostics', 'list'])
    expect(diagnosticKeys.detail('e1')).toEqual(['logs', 'diagnostics', 'detail', 'e1'])
  })
})

describe('diagnosticQueries', () => {
  it('loads one entry with its exception', async () => {
    server.use(
      http.get('/api/logs/diagnostics/:id', ({ params }) =>
        HttpResponse.json(buildDiagnosticDetail({ id: String(params.id) })),
      ),
    )

    await expect(
      createTestQueryClient().fetchQuery(diagnosticQueries.detail('e1')),
    ).resolves.toMatchObject({ id: 'e1', exception: expect.stringContaining('boom') as string })
  })
})

describe('diagnosticList', () => {
  it('sends the filters, sort and page as query and maps the rows', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/logs/diagnostics', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildDiagnosticEntry()], 31))
      }),
    )

    const page = await diagnosticList.fetchPage({
      search: 'boom',
      level: 'Error',
      category: 'Handler',
      traceId: 'abc',
      occurredFrom: '2026-07-01',
      occurredTo: '2026-07-04',
      page: 2,
      pageSize: 25,
      sort: '-occurredAt',
    })

    expect(page.total).toBe(31)
    expect(page.items[0]?.eventName).toBe('UnhandledRequestException')
    expect(queryOf(urls[0] ?? '')).toEqual({
      search: 'boom',
      level: 'Error',
      category: 'Handler',
      traceId: 'abc',
      occurredFrom: '2026-07-01',
      occurredTo: '2026-07-04',
      page: '2',
      pageSize: '25',
      sort: '-occurredAt',
    })
  })
})
