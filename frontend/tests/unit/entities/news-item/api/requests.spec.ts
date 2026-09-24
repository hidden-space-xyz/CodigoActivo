import { describe, expect, it } from 'vitest'

import {
  createNewsItemRequest,
  deleteNewsItemRequest,
  getNewsAdminPageRequest,
  getNewsItemAdminRequest,
  toggleNewsItemFeatureRequest,
  updateNewsItemRequest,
} from '@/entities/news-item'
import {
  getHomeNewsRequest,
  getNewsByYearPageRequest,
  getNewsItemByIdRequest,
  getNewsYearsRequest,
} from '@/entities/news-item/api/requests'
import { ApiError, FEATURED_FIRST_SORT } from '@/shared/api'
import type { NewsItemResponse, NewsListItemResponse } from '@/shared/api/generated/models'

import { apiError, http, HttpResponse, paged, server } from '../../../../support/server'

function queryOf(url: URL | undefined): Record<string, string> {
  return Object.fromEntries(url?.searchParams ?? [])
}

describe('news requests', () => {
  it('lists the available years as strings', async () => {
    server.use(http.get('/api/news/years', () => HttpResponse.json([2026, 2025])))

    await expect(getNewsYearsRequest()).resolves.toEqual(['2026', '2025'])
  })

  it('returns no years when the body is empty', async () => {
    server.use(http.get('/api/news/years', () => new HttpResponse(null, { status: 204 })))

    await expect(getNewsYearsRequest()).resolves.toEqual([])
  })

  it('pages a year newest first and sends the search only when present', async () => {
    const urls: URL[] = []
    const items: NewsListItemResponse[] = [{ id: 'a1', title: 'Uno' }]
    server.use(
      http.get('/api/news', ({ request }) => {
        urls.push(new URL(request.url))
        return HttpResponse.json(paged(items, 30))
      }),
    )

    const page = await getNewsByYearPageRequest('2026', 'robot', 2, 25)
    await getNewsByYearPageRequest('2025', '', 1, 10)

    expect(page.total).toBe(30)
    expect(page.items).toEqual([expect.objectContaining({ id: 'a1', title: 'Uno' })])
    expect(queryOf(urls[0])).toEqual({
      year: '2026',
      search: 'robot',
      sort: '-createdAt',
      page: '2',
      pageSize: '25',
    })
    expect(queryOf(urls[1])).toEqual({
      year: '2025',
      sort: '-createdAt',
      page: '1',
      pageSize: '10',
    })
  })

  it('splits the first home news item off as the featured one', async () => {
    let url: URL | undefined
    const items: NewsListItemResponse[] = [{ id: 'a1', featured: true }, { id: 'a2' }, { id: 'a3' }]
    server.use(
      http.get('/api/news', ({ request }) => {
        url = new URL(request.url)
        return HttpResponse.json(paged(items))
      }),
    )

    const home = await getHomeNewsRequest()

    expect(home.featured?.id).toBe('a1')
    expect(home.items.map((item) => item.id)).toEqual(['a2', 'a3'])
    expect(queryOf(url)).toEqual({ sort: FEATURED_FIRST_SORT, pageSize: '4' })
  })

  it('returns no featured news item when there are none', async () => {
    server.use(http.get('/api/news', () => HttpResponse.json({})))

    await expect(getHomeNewsRequest()).resolves.toEqual({ featured: null, items: [] })
  })

  describe('detail requests', () => {
    it('maps the public detail and returns the raw admin detail', async () => {
      const newsItem: NewsItemResponse = { id: 'a1', title: 'Uno', description: 'Texto' }
      server.use(http.get('/api/news/a1', () => HttpResponse.json(newsItem)))

      await expect(getNewsItemByIdRequest('a1')).resolves.toMatchObject({
        id: 'a1',
        description: 'Texto',
        publishedAt: null,
      })
      await expect(getNewsItemAdminRequest('a1')).resolves.toEqual(newsItem)
    })

    it('resolves null for a missing news item', async () => {
      server.use(http.get('/api/news/missing', () => apiError(404, 'NewsItemNotFound')))

      await expect(getNewsItemByIdRequest('missing')).resolves.toBeNull()
      await expect(getNewsItemAdminRequest('missing')).resolves.toBeNull()
    })

    it('rethrows other errors', async () => {
      server.use(http.get('/api/news/broken', () => apiError(500)))

      await expect(getNewsItemByIdRequest('broken')).rejects.toBeInstanceOf(ApiError)
    })
  })

  it('pages the admin table with the given params', async () => {
    let url: URL | undefined
    server.use(
      http.get('/api/news', ({ request }) => {
        url = new URL(request.url)
        return HttpResponse.json(paged([{ id: 'a1' }], 12))
      }),
    )

    await expect(getNewsAdminPageRequest({ page: 3, pageSize: 5, sort: 'title' })).resolves.toEqual(
      { items: [{ id: 'a1' }], total: 12 },
    )
    expect(queryOf(url)).toEqual({ page: '3', pageSize: '5', sort: 'title' })
  })

  it('creates, updates, features and deletes news items', async () => {
    const calls: { method: string; path: string; body: unknown }[] = []
    const record = async ({ request }: { request: Request }) => {
      const text = await request.text()
      calls.push({
        method: request.method,
        path: new URL(request.url).pathname,
        body: text ? (JSON.parse(text) as unknown) : null,
      })
      return request.method === 'DELETE'
        ? new HttpResponse(null, { status: 204 })
        : HttpResponse.json<NewsItemResponse>({ id: 'a1', title: 'Saved' })
    }
    server.use(
      http.post('/api/news', record),
      http.put('/api/news/a1', record),
      http.patch('/api/news/a1/feature', record),
      http.delete('/api/news/a1', record),
    )

    const created = await createNewsItemRequest({ title: 'Nuevo', thumbnailId: 't1' })
    await updateNewsItemRequest('a1', { title: 'Editado' })
    await toggleNewsItemFeatureRequest('a1')
    const deleted = await deleteNewsItemRequest('a1')

    expect(created.data).toEqual({ id: 'a1', title: 'Saved' })
    expect(deleted.status).toBe(204)
    expect(calls).toEqual([
      { method: 'POST', path: '/api/news', body: { title: 'Nuevo', thumbnailId: 't1' } },
      { method: 'PUT', path: '/api/news/a1', body: { title: 'Editado' } },
      { method: 'PATCH', path: '/api/news/a1/feature', body: null },
      { method: 'DELETE', path: '/api/news/a1', body: null },
    ])
  })
})
