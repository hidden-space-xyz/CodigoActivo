import { describe, expect, it } from 'vitest'

import {
  createAnnouncementRequest,
  deleteAnnouncementRequest,
  getAnnouncementAdminRequest,
  getAnnouncementsAdminPageRequest,
  toggleAnnouncementFeatureRequest,
  updateAnnouncementRequest,
} from '@/entities/announcement'
import {
  getAnnouncementByIdRequest,
  getAnnouncementsByYearPageRequest,
  getAnnouncementYearsRequest,
  getHomeAnnouncementsRequest,
} from '@/entities/announcement/api/requests'
import { ApiError, FEATURED_FIRST_SORT } from '@/shared/api'
import type {
  AnnouncementListItemResponse,
  AnnouncementResponse,
} from '@/shared/api/generated/models'

import { apiError, http, HttpResponse, paged, server } from '../../../../support/server'

function queryOf(url: URL | undefined): Record<string, string> {
  return Object.fromEntries(url?.searchParams ?? [])
}

describe('announcement requests', () => {
  it('lists the available years as strings', async () => {
    server.use(http.get('/api/announcements/years', () => HttpResponse.json([2026, 2025])))

    await expect(getAnnouncementYearsRequest()).resolves.toEqual(['2026', '2025'])
  })

  it('returns no years when the body is empty', async () => {
    server.use(http.get('/api/announcements/years', () => new HttpResponse(null, { status: 204 })))

    await expect(getAnnouncementYearsRequest()).resolves.toEqual([])
  })

  it('pages a year newest first and sends the search only when present', async () => {
    const urls: URL[] = []
    const items: AnnouncementListItemResponse[] = [{ id: 'a1', title: 'Uno' }]
    server.use(
      http.get('/api/announcements', ({ request }) => {
        urls.push(new URL(request.url))
        return HttpResponse.json(paged(items, 30))
      }),
    )

    const page = await getAnnouncementsByYearPageRequest('2026', 'robot', 2, 25)
    await getAnnouncementsByYearPageRequest('2025', '', 1, 10)

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

  it('splits the first home announcement off as the featured one', async () => {
    let url: URL | undefined
    const items: AnnouncementListItemResponse[] = [
      { id: 'a1', featured: true },
      { id: 'a2' },
      { id: 'a3' },
    ]
    server.use(
      http.get('/api/announcements', ({ request }) => {
        url = new URL(request.url)
        return HttpResponse.json(paged(items))
      }),
    )

    const home = await getHomeAnnouncementsRequest()

    expect(home.featured?.id).toBe('a1')
    expect(home.items.map((item) => item.id)).toEqual(['a2', 'a3'])
    expect(queryOf(url)).toEqual({ sort: FEATURED_FIRST_SORT, pageSize: '4' })
  })

  it('returns no featured announcement when there are none', async () => {
    server.use(http.get('/api/announcements', () => HttpResponse.json({})))

    await expect(getHomeAnnouncementsRequest()).resolves.toEqual({ featured: null, items: [] })
  })

  describe('detail requests', () => {
    it('maps the public detail and returns the raw admin detail', async () => {
      const announcement: AnnouncementResponse = { id: 'a1', title: 'Uno', description: 'Texto' }
      server.use(http.get('/api/announcements/a1', () => HttpResponse.json(announcement)))

      await expect(getAnnouncementByIdRequest('a1')).resolves.toMatchObject({
        id: 'a1',
        description: 'Texto',
        publishedAt: null,
      })
      await expect(getAnnouncementAdminRequest('a1')).resolves.toEqual(announcement)
    })

    it('resolves null for a missing announcement', async () => {
      server.use(
        http.get('/api/announcements/missing', () => apiError(404, 'AnnouncementNotFound')),
      )

      await expect(getAnnouncementByIdRequest('missing')).resolves.toBeNull()
      await expect(getAnnouncementAdminRequest('missing')).resolves.toBeNull()
    })

    it('rethrows other errors', async () => {
      server.use(http.get('/api/announcements/broken', () => apiError(500)))

      await expect(getAnnouncementByIdRequest('broken')).rejects.toBeInstanceOf(ApiError)
    })
  })

  it('pages the admin table with the given params', async () => {
    let url: URL | undefined
    server.use(
      http.get('/api/announcements', ({ request }) => {
        url = new URL(request.url)
        return HttpResponse.json(paged([{ id: 'a1' }], 12))
      }),
    )

    await expect(
      getAnnouncementsAdminPageRequest({ page: 3, pageSize: 5, sort: 'title' }),
    ).resolves.toEqual({ items: [{ id: 'a1' }], total: 12 })
    expect(queryOf(url)).toEqual({ page: '3', pageSize: '5', sort: 'title' })
  })

  it('creates, updates, features and deletes announcements', async () => {
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
        : HttpResponse.json<AnnouncementResponse>({ id: 'a1', title: 'Saved' })
    }
    server.use(
      http.post('/api/announcements', record),
      http.put('/api/announcements/a1', record),
      http.patch('/api/announcements/a1/feature', record),
      http.delete('/api/announcements/a1', record),
    )

    const created = await createAnnouncementRequest({ title: 'Nuevo', thumbnailId: 't1' })
    await updateAnnouncementRequest('a1', { title: 'Editado' })
    await toggleAnnouncementFeatureRequest('a1')
    const deleted = await deleteAnnouncementRequest('a1')

    expect(created.data).toEqual({ id: 'a1', title: 'Saved' })
    expect(deleted.status).toBe(204)
    expect(calls).toEqual([
      { method: 'POST', path: '/api/announcements', body: { title: 'Nuevo', thumbnailId: 't1' } },
      { method: 'PUT', path: '/api/announcements/a1', body: { title: 'Editado' } },
      { method: 'PATCH', path: '/api/announcements/a1/feature', body: null },
      { method: 'DELETE', path: '/api/announcements/a1', body: null },
    ])
  })
})
