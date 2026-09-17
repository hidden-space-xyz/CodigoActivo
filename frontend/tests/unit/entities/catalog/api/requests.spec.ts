import { describe, expect, it } from 'vitest'

import {
  createEventCategoryTypeRequest,
  createTermsDocumentRequest,
  deleteEventCategoryTypeRequest,
  deleteTermsDocumentRequest,
  getEventCategoryTypesPageRequest,
  getTermsDocumentsPageRequest,
  updateEventCategoryTypeRequest,
  updateTermsDocumentRequest,
} from '@/entities/catalog'
import {
  getActivityModalityTypesRequest,
  getActivityRoleTypesRequest,
  getAssignmentStatusTypesRequest,
  getEventCategoryTypesRequest,
  getResourceTypesRequest,
  getTermsDocumentsRequest,
  getUserStatusTypesRequest,
  getUserTypesRequest,
} from '@/entities/catalog/api/requests'

import { http, HttpResponse, paged, server } from '../../../../support/server'

const simpleCatalogs = [
  ['/api/users/types', getUserTypesRequest],
  ['/api/users/status-types', getUserStatusTypesRequest],
  ['/api/activities/roleType', getActivityRoleTypesRequest],
  ['/api/activities/assignment-status-types', getAssignmentStatusTypesRequest],
  ['/api/activities/modality-types', getActivityModalityTypesRequest],
  ['/api/resources/types', getResourceTypesRequest],
] as const

describe('catalog requests', () => {
  it.each(simpleCatalogs)('returns the %s catalog as an array', async (path, request) => {
    const items = [{ id: 'c1', name: 'Uno' }]
    server.use(http.get(path, () => HttpResponse.json(items)))

    await expect(request()).resolves.toEqual(items)
  })

  it.each(simpleCatalogs)('returns an empty %s catalog without a body', async (path, request) => {
    server.use(http.get(path, () => new HttpResponse(null, { status: 204 })))

    await expect(request()).resolves.toEqual([])
  })

  it.each([
    ['/api/events/categoryType', getEventCategoryTypesRequest],
    ['/api/events/termsDocument', getTermsDocumentsRequest],
  ] as const)('loads up to 100 items from %s as a plain array', async (path, request) => {
    let pageSize: string | null = null
    server.use(
      http.get(path, ({ request: httpRequest }) => {
        pageSize = new URL(httpRequest.url).searchParams.get('pageSize')
        return HttpResponse.json(paged([{ id: 'c1', name: 'Uno' }], 40))
      }),
    )

    await expect(request()).resolves.toEqual([{ id: 'c1', name: 'Uno' }])
    expect(pageSize).toBe('100')

    server.use(http.get(path, () => HttpResponse.json({ total: 0 })))
    await expect(request()).resolves.toEqual([])
  })

  it('pages the event categories and terms documents admin tables', async () => {
    const queries: Record<string, string>[] = []
    const capture = ({ request }: { request: Request }) => {
      queries.push(Object.fromEntries(new URL(request.url).searchParams))
      return HttpResponse.json(paged([{ id: 'x' }], 11))
    }
    server.use(
      http.get('/api/events/categoryType', capture),
      http.get('/api/events/termsDocument', capture),
    )

    await expect(getEventCategoryTypesPageRequest({ page: 2, pageSize: 10 })).resolves.toEqual({
      items: [{ id: 'x' }],
      total: 11,
    })
    await expect(getTermsDocumentsPageRequest({ page: 1, sort: 'name' })).resolves.toEqual({
      items: [{ id: 'x' }],
      total: 11,
    })
    expect(queries).toEqual([
      { page: '2', pageSize: '10' },
      { page: '1', sort: 'name' },
    ])
  })

  it('creates, updates and deletes event categories and terms documents', async () => {
    const calls: { method: string; path: string; body: unknown }[] = []
    const record = async ({ request }: { request: Request }) => {
      const text = await request.text()
      calls.push({
        method: request.method,
        path: new URL(request.url).pathname,
        body: text ? (JSON.parse(text) as unknown) : null,
      })
      return request.method === 'POST'
        ? HttpResponse.json({ id: 'created' })
        : new HttpResponse(null, { status: 204 })
    }
    server.use(
      http.post('/api/events/categoryType', record),
      http.put('/api/events/categoryType/cat-1', record),
      http.delete('/api/events/categoryType/cat-1', record),
      http.post('/api/events/termsDocument', record),
      http.put('/api/events/termsDocument/terms-1', record),
      http.delete('/api/events/termsDocument/terms-1', record),
    )

    await expect(
      createEventCategoryTypeRequest({ name: 'Robótica', color: '#ff0000' }),
    ).resolves.toEqual({ id: 'created' })
    expect((await updateEventCategoryTypeRequest('cat-1', { name: 'IA' })).status).toBe(204)
    expect((await deleteEventCategoryTypeRequest('cat-1')).status).toBe(204)
    await expect(createTermsDocumentRequest({ name: 'Normas' })).resolves.toEqual({
      id: 'created',
    })
    expect(
      (await updateTermsDocumentRequest('terms-1', { name: 'Normas', description: 'Texto' }))
        .status,
    ).toBe(204)
    expect((await deleteTermsDocumentRequest('terms-1')).status).toBe(204)

    expect(calls).toEqual([
      {
        method: 'POST',
        path: '/api/events/categoryType',
        body: { name: 'Robótica', color: '#ff0000' },
      },
      { method: 'PUT', path: '/api/events/categoryType/cat-1', body: { name: 'IA' } },
      { method: 'DELETE', path: '/api/events/categoryType/cat-1', body: null },
      { method: 'POST', path: '/api/events/termsDocument', body: { name: 'Normas' } },
      {
        method: 'PUT',
        path: '/api/events/termsDocument/terms-1',
        body: { name: 'Normas', description: 'Texto' },
      },
      { method: 'DELETE', path: '/api/events/termsDocument/terms-1', body: null },
    ])
  })
})
