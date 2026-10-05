import { describe, expect, it } from 'vitest'

import type { EventInput } from '@/entities/event'
import {
  createEventRequest,
  deleteEventRequest,
  featureEventRequest,
  getEventLeaderRosterRequest,
  getEventRequest,
  getEventsPageRequest,
  getEventTermsStateRequest,
  getHomeEventsRequest,
  getPastEventCategoriesRequest,
  getPastEventsPageRequest,
  getPastEventYearsRequest,
  getUpcomingEventsPageRequest,
  updateEventRequest,
} from '@/entities/event/api/requests'
import { ApiError } from '@/shared/api'
import type {
  EventCategoryTypeResponse,
  EventListItemResponse,
  EventResponse,
  EventTermsStateResponse,
} from '@/shared/api/generated/models'

import { apiError, http, HttpResponse, noContent, paged, server } from '../../../../support/server'
import {
  buildEventListItem,
  buildEventResponse,
  buildLeaderRosterActivity,
  buildTermsDocumentState,
} from '../../../../support/builders'

function queryOf(request: Request): Record<string, string> {
  return Object.fromEntries(new URL(request.url).searchParams)
}

describe('event requests', () => {
  it('pages upcoming events by start date and maps them to cards', async () => {
    let query: Record<string, string> = {}
    const items: EventListItemResponse[] = [
      buildEventListItem({ id: 'e1', title: 'Día', subtitle: 'Lema', stage: 'Upcoming' }),
    ]
    server.use(
      http.get('/api/events', ({ request }) => {
        query = queryOf(request)
        return HttpResponse.json(paged(items, 9))
      }),
    )

    const page = await getUpcomingEventsPageRequest(2, 4)

    expect(page.total).toBe(9)
    expect(page.items).toEqual([
      expect.objectContaining({ id: 'e1', subtitle: 'Lema', status: 'upcoming' }),
    ])
    expect(query).toEqual({ scope: 'Upcoming', sort: 'eventStartsAt', page: '2', pageSize: '4' })
  })

  it('lists past years as strings', async () => {
    server.use(http.get('/api/events/past-years', () => HttpResponse.json([2025, 2024])))

    await expect(getPastEventYearsRequest()).resolves.toEqual(['2025', '2024'])
  })

  it('lists past categories as tags', async () => {
    const data: EventCategoryTypeResponse[] = [{ id: 'cat-1', name: 'IA', color: '#123456' }]
    server.use(http.get('/api/events/past-categories', () => HttpResponse.json(data)))

    await expect(getPastEventCategoriesRequest()).resolves.toEqual([
      { id: 'cat-1', name: 'IA', color: '#123456' },
    ])
  })

  it('pages past events newest first sending search and category only when set', async () => {
    const queries: Record<string, string>[] = []
    server.use(
      http.get('/api/events', ({ request }) => {
        queries.push(queryOf(request))
        return HttpResponse.json(paged([buildEventListItem({ id: 'e0', subtitle: 'Crea' })]))
      }),
    )

    const page = await getPastEventsPageRequest(
      { year: '2025', search: 'robot', categoryId: 'cat-1' },
      1,
      25,
    )
    await getPastEventsPageRequest({ year: '2024', search: '', categoryId: '' }, 2, 10)

    expect(page.items).toEqual([
      expect.objectContaining({ id: 'e0', subtitle: 'Crea', status: 'finished' }),
    ])
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

  describe('getEventRequest', () => {
    it('maps the whole event', async () => {
      const event: EventResponse = buildEventResponse({
        id: 'e1',
        title: 'Día',
        description: 'Texto',
      })
      server.use(http.get('/api/events/e1', () => HttpResponse.json(event)))

      await expect(getEventRequest('e1')).resolves.toMatchObject({
        id: 'e1',
        description: 'Texto',
        status: 'signupOpen',
        terms: [],
      })
    })

    it('resolves null for a missing event', async () => {
      server.use(http.get('/api/events/missing', () => apiError(404, 'EventNotFound')))

      await expect(getEventRequest('missing')).resolves.toBeNull()
    })

    it('rethrows other errors', async () => {
      server.use(http.get('/api/events/broken', () => apiError(500)))

      await expect(getEventRequest('broken')).rejects.toBeInstanceOf(ApiError)
    })
  })

  it('loads the terms state sorted by display order', async () => {
    const state: EventTermsStateResponse = {
      documents: [
        buildTermsDocumentState({ termsDocumentId: 'terms-2', name: 'Segundo', displayOrder: 1 }),
        buildTermsDocumentState({ termsDocumentId: 'terms-1', name: 'Primero' }),
      ],
      signupBlocked: true,
    }
    server.use(http.get('/api/events/e1/terms', () => HttpResponse.json(state)))

    const result = await getEventTermsStateRequest('e1')

    expect(result.signupBlocked).toBe(true)
    expect(result.documents.map((document) => [document.id, document.name])).toEqual([
      ['terms-1', 'Primero'],
      ['terms-2', 'Segundo'],
    ])
  })

  it('loads the activities the user leads with their attendees', async () => {
    server.use(
      http.get('/api/events/e1/leader-roster', () =>
        HttpResponse.json([buildLeaderRosterActivity()]),
      ),
      http.get('/api/events/e2/leader-roster', () => HttpResponse.json([])),
    )

    const [activity] = await getEventLeaderRosterRequest('e1')

    expect(activity).toMatchObject({ id: 'act-1', title: 'Taller de robótica' })
    expect(activity?.roles.map((role) => role.id)).toEqual([
      'role-leader',
      'role-volunteer',
      'role-participant',
    ])
    await expect(getEventLeaderRosterRequest('e2')).resolves.toEqual([])
  })

  describe('getHomeEventsRequest', () => {
    it('combines the unfinished featured event with up to three other upcoming events', async () => {
      const queries: Record<string, string>[] = []
      server.use(
        http.get('/api/events', ({ request }) => {
          const query = queryOf(request)
          queries.push(query)
          if (query.featured === 'true') {
            return HttpResponse.json(
              paged([buildEventListItem({ id: 'e2', title: 'Destacado', featured: true })]),
            )
          }
          return HttpResponse.json(
            paged([
              buildEventListItem({ id: 'e1' }),
              buildEventListItem({ id: 'e2' }),
              buildEventListItem({ id: 'e3' }),
              buildEventListItem({ id: 'e4' }),
              buildEventListItem({ id: 'e5' }),
            ]),
          )
        }),
      )

      const home = await getHomeEventsRequest()

      expect(home.featured).toMatchObject({ id: 'e2', title: 'Destacado' })
      expect(home.items.map((event) => event.id)).toEqual(['e1', 'e3', 'e4'])
      expect(queries).toEqual(
        expect.arrayContaining([
          { featured: 'true', scope: 'Upcoming', pageSize: '1' },
          { scope: 'Upcoming', sort: 'eventStartsAt', pageSize: '4' },
        ]),
      )
    })

    it('returns no featured event and no items when the lists are empty', async () => {
      server.use(http.get('/api/events', () => HttpResponse.json(paged([]))))

      await expect(getHomeEventsRequest()).resolves.toEqual({ featured: null, items: [] })
    })
  })

  it('pages the admin table, mapping each event to a row', async () => {
    let query: Record<string, string> = {}
    server.use(
      http.get('/api/events', ({ request }) => {
        query = queryOf(request)
        return HttpResponse.json(
          paged(
            [
              buildEventListItem({
                id: 'e1',
                featured: true,
                earlySignupStartsAt: '2026-08-20T08:00:00Z',
              }),
            ],
            3,
          ),
        )
      }),
    )

    const page = await getEventsPageRequest({ page: 1, pageSize: 20, sort: 'title' })

    expect(page.total).toBe(3)
    expect(page.items[0]).toMatchObject({
      id: 'e1',
      featured: true,
      earlySignupStartsAt: '2026-08-20T08:00:00Z',
      signupStartsAt: expect.any(String) as string,
      status: 'signupOpen',
    })
    expect(query).toEqual({ page: '1', pageSize: '20', sort: 'title' })
  })

  it('creates, updates, features and deletes events', async () => {
    const input: EventInput = {
      title: 'Nuevo',
      subtitle: 'Sub',
      description: '<p>Texto</p>',
      startsAt: '2026-10-10',
      endsAt: '2026-10-11',
      earlySignupStartsAt: null,
      signupStartsAt: '2026-09-01T08:00:00.000Z',
      signupEndsAt: '2026-10-01T20:00:00.000Z',
      thumbnailId: 't1',
      categoryIds: ['cat-1'],
      terms: [{ documentId: 'terms-1', required: false }],
    }
    const body = {
      title: 'Nuevo',
      subtitle: 'Sub',
      description: '<p>Texto</p>',
      eventStartsAt: '2026-10-10',
      eventEndsAt: '2026-10-11',
      earlySignupStartsAt: null,
      signupStartsAt: '2026-09-01T08:00:00.000Z',
      signupEndsAt: '2026-10-01T20:00:00.000Z',
      thumbnailId: 't1',
      categoryTypeIds: ['cat-1'],
      termsDocuments: [{ termsDocumentId: 'terms-1', required: false }],
    }
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
        : HttpResponse.json<EventResponse>(buildEventResponse({ id: 'e1' }))
    }
    server.use(
      http.post('/api/events', record),
      http.put('/api/events/e1', record),
      http.patch('/api/events/e1/feature', record),
      http.delete('/api/events/e1', record),
    )

    await createEventRequest(input)
    await updateEventRequest('e1', { ...input, terms: [] })
    await featureEventRequest('e1')
    await deleteEventRequest('e1')

    expect(calls).toEqual([
      { method: 'POST', path: '/api/events', body },
      { method: 'PUT', path: '/api/events/e1', body: { ...body, termsDocuments: null } },
      { method: 'PATCH', path: '/api/events/e1/feature', body: null },
      { method: 'DELETE', path: '/api/events/e1', body: null },
    ])
  })
})
