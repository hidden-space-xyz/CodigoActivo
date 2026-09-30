import { describe, expect, it } from 'vitest'

import {
  createEventCategoryRequest,
  deleteEventCategoryRequest,
  getEventCategoriesRequest,
  getEventCategoryListPageRequest,
  updateEventCategoryRequest,
} from '@/entities/event-category/api/requests'

import { http, HttpResponse, noContent, paged, server } from '../../../../support/server'
import { buildEventCategoryType } from '../../../../support/builders'
import { queryOf } from '../../../../support/dom'

const WORKSHOP = { id: 'category-1', name: 'Workshop', color: '#FF0000' }

describe('event category requests', () => {
  it('loads up to 100 categories for selectors', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/events/categoryType', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildEventCategoryType()], 40))
      }),
    )

    await expect(getEventCategoriesRequest()).resolves.toEqual([WORKSHOP])
    expect(queryOf(urls[0] ?? '')).toEqual({ pageSize: '100' })
  })

  it('pages the admin list', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/events/categoryType', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildEventCategoryType()], 11))
      }),
    )

    await expect(getEventCategoryListPageRequest({ page: 2, pageSize: 10 })).resolves.toEqual({
      items: [WORKSHOP],
      total: 11,
    })
    expect(queryOf(urls[0] ?? '')).toEqual({ page: '2', pageSize: '10' })
  })

  it('creates, updates and deletes categories', async () => {
    const calls: { method: string; path: string; body: unknown }[] = []
    const record = async ({ request }: { request: Request }) => {
      const text = await request.text()
      calls.push({
        method: request.method,
        path: new URL(request.url).pathname,
        body: text ? (JSON.parse(text) as unknown) : null,
      })
      return request.method === 'POST'
        ? HttpResponse.json(buildEventCategoryType({ id: 'created', name: 'Robótica' }))
        : noContent()
    }
    server.use(
      http.post('/api/events/categoryType', record),
      http.put('/api/events/categoryType/cat-1', record),
      http.delete('/api/events/categoryType/cat-1', record),
    )

    await expect(
      createEventCategoryRequest({ name: 'Robótica', color: '#ff0000' }),
    ).resolves.toMatchObject({ id: 'created', name: 'Robótica' })
    await updateEventCategoryRequest('cat-1', { name: 'IA', color: '#00ff00' })
    await deleteEventCategoryRequest('cat-1')

    expect(calls).toEqual([
      {
        method: 'POST',
        path: '/api/events/categoryType',
        body: { name: 'Robótica', color: '#ff0000' },
      },
      {
        method: 'PUT',
        path: '/api/events/categoryType/cat-1',
        body: { name: 'IA', color: '#00ff00' },
      },
      { method: 'DELETE', path: '/api/events/categoryType/cat-1', body: null },
    ])
  })
})
