import { describe, expect, it } from 'vitest'

import type { NewsItemInput } from '@/entities/news-item'
import {
  createNewsItemRequest,
  deleteNewsItemRequest,
  featureNewsItemRequest,
  getHomeNewsRequest,
  getNewsByYearPageRequest,
  getNewsItemRequest,
  getNewsListPageRequest,
  getNewsYearsRequest,
  updateNewsItemRequest,
} from '@/entities/news-item/api/requests'
import { ApiError, FEATURED_FIRST_SORT } from '@/shared/api'

import { apiError, http, HttpResponse, noContent, paged, server } from '../../../../support/server'
import { buildNewsItemResponse, buildNewsListItem, richText } from '../../../../support/builders'
import { queryOf } from '../../../../support/dom'

const INPUT: NewsItemInput = {
  title: 'Nuevo',
  subtitle: 'Viernes',
  description: richText('Texto'),
  thumbnailId: 't1',
}

describe('news requests', () => {
  it('lists the available years as strings', async () => {
    server.use(http.get('/api/news/years', () => HttpResponse.json([2026, 2025])))

    await expect(getNewsYearsRequest()).resolves.toEqual(['2026', '2025'])
  })

  it('pages a year newest first and sends the search only when present', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/news', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildNewsListItem({ id: 'a1', title: 'Uno' })], 30))
      }),
    )

    const page = await getNewsByYearPageRequest('2026', 'robot', 2, 25)
    await getNewsByYearPageRequest('2025', '', 1, 10)

    expect(page.total).toBe(30)
    expect(page.items).toEqual([expect.objectContaining({ id: 'a1', title: 'Uno' })])
    expect(urls.map(queryOf)).toEqual([
      { year: '2026', search: 'robot', sort: '-createdAt', page: '2', pageSize: '25' },
      { year: '2025', sort: '-createdAt', page: '1', pageSize: '10' },
    ])
  })

  it('splits the first home news item off as the featured one', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/news', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(
          paged(['a1', 'a2', 'a3'].map((id) => buildNewsListItem({ id, featured: id === 'a1' }))),
        )
      }),
    )

    const home = await getHomeNewsRequest()

    expect(home.featured?.id).toBe('a1')
    expect(home.items.map((item) => item.id)).toEqual(['a2', 'a3'])
    expect(queryOf(urls[0] ?? '')).toEqual({ sort: FEATURED_FIRST_SORT, pageSize: '4' })
  })

  it('returns no featured news item when there are none', async () => {
    server.use(http.get('/api/news', () => HttpResponse.json(paged([]))))

    await expect(getHomeNewsRequest()).resolves.toEqual({ featured: null, items: [] })
  })

  it('loads a whole news item, resolving null when it does not exist', async () => {
    server.use(
      http.get('/api/news/a1', () => HttpResponse.json(buildNewsItemResponse({ id: 'a1' }))),
      http.get('/api/news/missing', () => apiError(404, 'NewsItemNotFound')),
      http.get('/api/news/broken', () => apiError(500)),
    )

    await expect(getNewsItemRequest('a1')).resolves.toMatchObject({
      id: 'a1',
      description: richText('Ya puedes apuntarte.'),
    })
    await expect(getNewsItemRequest('missing')).resolves.toBeNull()
    await expect(getNewsItemRequest('broken')).rejects.toBeInstanceOf(ApiError)
  })

  it('pages the admin list with the given params', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/news', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildNewsListItem()], 12))
      }),
    )

    await expect(getNewsListPageRequest({ page: 3, pageSize: 5, sort: 'title' })).resolves.toEqual({
      items: [expect.objectContaining({ id: 'news-item-1' })],
      total: 12,
    })
    expect(queryOf(urls[0] ?? '')).toEqual({ page: '3', pageSize: '5', sort: 'title' })
  })

  it('creates, replaces, features and deletes news items', async () => {
    const calls: { method: string; path: string; body: unknown }[] = []
    const record = async ({ request }: { request: Request }) => {
      const text = await request.text()
      calls.push({
        method: request.method,
        path: new URL(request.url).pathname,
        body: text ? (JSON.parse(text) as unknown) : null,
      })
      return request.method === 'POST' || request.method === 'PUT'
        ? HttpResponse.json(buildNewsItemResponse())
        : noContent()
    }
    server.use(
      http.post('/api/news', record),
      http.put('/api/news/a1', record),
      http.patch('/api/news/a1/feature', record),
      http.delete('/api/news/a1', record),
    )

    await createNewsItemRequest(INPUT)
    await updateNewsItemRequest('a1', { ...INPUT, title: 'Editado' })
    await featureNewsItemRequest('a1')
    await deleteNewsItemRequest('a1')

    expect(calls).toEqual([
      { method: 'POST', path: '/api/news', body: INPUT },
      { method: 'PUT', path: '/api/news/a1', body: { ...INPUT, title: 'Editado' } },
      { method: 'PATCH', path: '/api/news/a1/feature', body: null },
      { method: 'DELETE', path: '/api/news/a1', body: null },
    ])
  })
})
