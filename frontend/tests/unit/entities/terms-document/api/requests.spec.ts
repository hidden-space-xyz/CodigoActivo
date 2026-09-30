import { describe, expect, it } from 'vitest'

import {
  createTermsDocumentRequest,
  deleteTermsDocumentRequest,
  getTermsDocumentListPageRequest,
  getTermsDocumentsRequest,
  updateTermsDocumentRequest,
} from '@/entities/terms-document/api/requests'

import { http, HttpResponse, noContent, paged, server } from '../../../../support/server'
import { buildTermsDocument } from '../../../../support/builders'
import { queryOf } from '../../../../support/dom'

describe('terms document requests', () => {
  it('loads up to 100 terms documents for the event form', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/events/termsDocument', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildTermsDocument()], 40))
      }),
    )

    await expect(getTermsDocumentsRequest()).resolves.toEqual([
      expect.objectContaining({ id: 'terms-1', name: 'Privacy' }),
    ])
    expect(queryOf(urls[0] ?? '')).toEqual({ pageSize: '100' })
  })

  it('pages the admin list', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/events/termsDocument', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildTermsDocument()], 11))
      }),
    )

    await expect(getTermsDocumentListPageRequest({ page: 1, sort: 'name' })).resolves.toMatchObject(
      { total: 11 },
    )
    expect(queryOf(urls[0] ?? '')).toEqual({ page: '1', sort: 'name' })
  })

  it('creates, updates and deletes terms documents', async () => {
    const calls: { method: string; path: string; body: unknown }[] = []
    const record = async ({ request }: { request: Request }) => {
      const text = await request.text()
      calls.push({
        method: request.method,
        path: new URL(request.url).pathname,
        body: text ? (JSON.parse(text) as unknown) : null,
      })
      return request.method === 'POST' ? HttpResponse.json(buildTermsDocument()) : noContent()
    }
    server.use(
      http.post('/api/events/termsDocument', record),
      http.put('/api/events/termsDocument/terms-1', record),
      http.delete('/api/events/termsDocument/terms-1', record),
    )

    await createTermsDocumentRequest({ name: 'Normas', description: 'Texto' })
    await updateTermsDocumentRequest('terms-1', { name: 'Normas', description: 'Otro' })
    await deleteTermsDocumentRequest('terms-1')

    expect(calls).toEqual([
      {
        method: 'POST',
        path: '/api/events/termsDocument',
        body: { name: 'Normas', description: 'Texto' },
      },
      {
        method: 'PUT',
        path: '/api/events/termsDocument/terms-1',
        body: { name: 'Normas', description: 'Otro' },
      },
      { method: 'DELETE', path: '/api/events/termsDocument/terms-1', body: null },
    ])
  })
})
