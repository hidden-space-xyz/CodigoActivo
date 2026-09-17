import { describe, expect, it } from 'vitest'

import {
  createEventRequest,
  deleteEventRequest,
  getDashboardAnalyticsRequest,
  getEventAdminRequest,
  getEventAttendeesPageRequest,
  getEventBadgesRequest,
  getEventRatingsPageRequest,
  getEventRosterRequest,
  getEventsAdminPageRequest,
  getEventSummaryRequest,
  getEventTermsAcceptanceRequest,
  toggleEventFeatureRequest,
  updateEventRequest,
} from '@/entities/event'
import {
  getEventByIdRequest,
  getHomeEventsRequest,
  getPastEventCategoriesRequest,
  getPastEventsPageRequest,
  getPastEventYearsRequest,
  getUpcomingEventsPageRequest,
} from '@/entities/event/api/requests'
import { ApiError, FEATURED_FIRST_SORT } from '@/shared/api'
import type {
  EventCategoryTypeResponse,
  EventListItemResponse,
  EventResponse,
} from '@/shared/api/generated/models'

import { apiError, http, HttpResponse, paged, server } from '../../../../support/server'

function queryOf(request: Request): Record<string, string> {
  return Object.fromEntries(new URL(request.url).searchParams)
}

const noContent = () => new HttpResponse(null, { status: 204 })

describe('event requests', () => {
  it('pages upcoming events by start date and maps them to cards', async () => {
    let query: Record<string, string> = {}
    const items: EventListItemResponse[] = [{ id: 'e1', title: 'Día', subtitle: 'Lema' }]
    server.use(
      http.get('/api/events', ({ request }) => {
        query = queryOf(request)
        return HttpResponse.json(paged(items, 9))
      }),
    )

    const page = await getUpcomingEventsPageRequest(2, 4)

    expect(page.total).toBe(9)
    expect(page.items).toEqual([expect.objectContaining({ id: 'e1', slogan: 'Lema' })])
    expect(query).toEqual({ scope: 'Upcoming', sort: 'eventStartsAt', page: '2', pageSize: '4' })
  })

  it('lists past years as strings and treats an empty body as none', async () => {
    server.use(http.get('/api/events/past-years', () => HttpResponse.json([2025, 2024])))
    await expect(getPastEventYearsRequest()).resolves.toEqual(['2025', '2024'])

    server.use(http.get('/api/events/past-years', noContent))
    await expect(getPastEventYearsRequest()).resolves.toEqual([])
  })

  it('lists past categories dropping entries without id', async () => {
    const data: EventCategoryTypeResponse[] = [
      { id: 'cat-1', name: 'IA', color: '#123456' },
      { name: 'Sin id' },
    ]
    server.use(http.get('/api/events/past-categories', () => HttpResponse.json(data)))
    await expect(getPastEventCategoriesRequest()).resolves.toEqual([
      { id: 'cat-1', name: 'IA', color: '#123456' },
    ])

    server.use(http.get('/api/events/past-categories', noContent))
    await expect(getPastEventCategoriesRequest()).resolves.toEqual([])
  })

  it('pages past events newest first sending search and category only when set', async () => {
    const queries: Record<string, string>[] = []
    server.use(
      http.get('/api/events', ({ request }) => {
        queries.push(queryOf(request))
        return HttpResponse.json(paged([{ id: 'e0', subtitle: 'Crea' }]))
      }),
    )

    const page = await getPastEventsPageRequest(
      { year: '2025', search: 'robot', categoryId: 'cat-1' },
      1,
      25,
    )
    await getPastEventsPageRequest({ year: '2024', search: '', categoryId: '' }, 2, 10)

    expect(page.items).toEqual([expect.objectContaining({ id: 'e0', eventName: 'Crea' })])
    expect(queries).toEqual([
      {
        scope: 'Past',
        year: '2025',
        search: 'robot',
        categoryTypeId: 'cat-1',
        sort: '-eventStartsAt',
        page: '1',
        pageSize: '25',
      },
      { scope: 'Past', year: '2024', sort: '-eventStartsAt', page: '2', pageSize: '10' },
    ])
  })

  describe('getEventByIdRequest and getEventAdminRequest', () => {
    it('maps the public detail and returns the raw admin event', async () => {
      const event: EventResponse = { id: 'e1', title: 'Día', description: 'Texto' }
      server.use(http.get('/api/events/e1', () => HttpResponse.json(event)))

      await expect(getEventByIdRequest('e1')).resolves.toMatchObject({
        id: 'e1',
        description: 'Texto',
        terms: null,
      })
      await expect(getEventAdminRequest('e1')).resolves.toEqual(event)
    })

    it('resolves null for a missing event', async () => {
      server.use(http.get('/api/events/missing', () => apiError(404, 'EventNotFound')))

      await expect(getEventByIdRequest('missing')).resolves.toBeNull()
      await expect(getEventAdminRequest('missing')).resolves.toBeNull()
    })

    it('rethrows other errors', async () => {
      server.use(http.get('/api/events/broken', () => apiError(500)))

      await expect(getEventByIdRequest('broken')).rejects.toBeInstanceOf(ApiError)
    })
  })

  it('reports whether the terms were accepted and defaults to not accepted', async () => {
    server.use(
      http.get('/api/events/e1/terms-acceptance', () => HttpResponse.json({ accepted: true })),
      http.get('/api/events/e2/terms-acceptance', () => HttpResponse.json({})),
    )

    await expect(getEventTermsAcceptanceRequest('e1')).resolves.toBe(true)
    await expect(getEventTermsAcceptanceRequest('e2')).resolves.toBe(false)
  })

  describe('getHomeEventsRequest', () => {
    it('combines the featured event with up to three other upcoming events', async () => {
      const queries: Record<string, string>[] = []
      server.use(
        http.get('/api/events', ({ request }) => {
          const query = queryOf(request)
          queries.push(query)
          if (query.sort === FEATURED_FIRST_SORT) {
            return HttpResponse.json(paged([{ id: 'e2', title: 'Destacado' }]))
          }
          return HttpResponse.json(
            paged([{ id: 'e1' }, { id: 'e2' }, { id: 'e3' }, { id: 'e4' }, { id: 'e5' }]),
          )
        }),
      )

      const home = await getHomeEventsRequest()

      expect(home.featured).toMatchObject({ id: 'e2', title: 'Destacado' })
      expect(home.items.map((event) => event.id)).toEqual(['e1', 'e3', 'e4'])
      expect(queries).toEqual(
        expect.arrayContaining([
          { sort: FEATURED_FIRST_SORT, pageSize: '1' },
          { scope: 'Upcoming', sort: 'eventStartsAt', pageSize: '4' },
        ]),
      )
    })

    it('returns no featured event and no items when the lists are empty', async () => {
      server.use(http.get('/api/events', () => HttpResponse.json({})))

      await expect(getHomeEventsRequest()).resolves.toEqual({ featured: null, items: [] })
    })
  })

  it('pages the admin table with raw items', async () => {
    let query: Record<string, string> = {}
    const items: EventListItemResponse[] = [{ id: 'e1', featured: true }]
    server.use(
      http.get('/api/events', ({ request }) => {
        query = queryOf(request)
        return HttpResponse.json(paged(items, 3))
      }),
    )

    await expect(
      getEventsAdminPageRequest({ page: 1, pageSize: 20, sort: 'title' }),
    ).resolves.toEqual({ items, total: 3 })
    expect(query).toEqual({ page: '1', pageSize: '20', sort: 'title' })
  })

  it('creates, updates, features and deletes events', async () => {
    const calls: { method: string; path: string; body: unknown }[] = []
    const record = async ({ request }: { request: Request }) => {
      const text = await request.text()
      calls.push({
        method: request.method,
        path: new URL(request.url).pathname,
        body: text ? (JSON.parse(text) as unknown) : null,
      })
      return request.method === 'DELETE'
        ? noContent()
        : HttpResponse.json<EventResponse>({ id: 'e1', title: request.method })
    }
    server.use(
      http.post('/api/events', record),
      http.put('/api/events/e1', record),
      http.patch('/api/events/e1/feature', record),
      http.delete('/api/events/e1', record),
    )

    await expect(createEventRequest({ title: 'Nuevo' })).resolves.toEqual({
      id: 'e1',
      title: 'POST',
    })
    await expect(updateEventRequest('e1', { title: 'Editado' })).resolves.toEqual({
      id: 'e1',
      title: 'PUT',
    })
    await expect(toggleEventFeatureRequest('e1')).resolves.toEqual({ id: 'e1', title: 'PATCH' })
    expect((await deleteEventRequest('e1')).status).toBe(204)

    expect(calls).toEqual([
      { method: 'POST', path: '/api/events', body: { title: 'Nuevo' } },
      { method: 'PUT', path: '/api/events/e1', body: { title: 'Editado' } },
      { method: 'PATCH', path: '/api/events/e1/feature', body: null },
      { method: 'DELETE', path: '/api/events/e1', body: null },
    ])
  })

  it('pages the ratings and attendees reports with their params', async () => {
    const queries: Record<string, string>[] = []
    server.use(
      http.get('/api/events/e1/ratings', ({ request }) => {
        queries.push(queryOf(request))
        return HttpResponse.json(paged([{ score: 5 }], 1))
      }),
      http.get('/api/reports/events/e1/attendees', ({ request }) => {
        queries.push(queryOf(request))
        return HttpResponse.json(paged([{ userId: 'u1' }], 2))
      }),
    )

    await expect(getEventRatingsPageRequest('e1', { page: 1, pageSize: 10 })).resolves.toEqual({
      items: [{ score: 5 }],
      total: 1,
    })
    await expect(getEventAttendeesPageRequest('e1', { page: 2 })).resolves.toEqual({
      items: [{ userId: 'u1' }],
      total: 2,
    })
    expect(queries).toEqual([{ page: '1', pageSize: '10' }, { page: '2' }])
  })

  it('loads the summary, badges, roster and dashboard analytics reports', async () => {
    let analyticsQuery: Record<string, string> = {}
    server.use(
      http.get('/api/reports/events/e1/summary', () => HttpResponse.json({ totalAttendees: 4 })),
      http.get('/api/reports/events/e1/badges', () => HttpResponse.json({ badges: [] })),
      http.get('/api/reports/events/e1/roster', () => HttpResponse.json({ activities: [] })),
      http.get('/api/reports/dashboard/analytics', ({ request }) => {
        analyticsQuery = queryOf(request)
        return HttpResponse.json({ kpis: [] })
      }),
    )

    await expect(getEventSummaryRequest('e1')).resolves.toEqual({ totalAttendees: 4 })
    await expect(getEventBadgesRequest('e1')).resolves.toEqual({ badges: [] })
    await expect(getEventRosterRequest('e1')).resolves.toEqual({ activities: [] })
    await expect(
      getDashboardAnalyticsRequest({ from: '2026-01-01', to: '2026-06-30' }),
    ).resolves.toEqual({ kpis: [] })
    expect(analyticsQuery).toEqual({ from: '2026-01-01', to: '2026-06-30' })
  })
})
