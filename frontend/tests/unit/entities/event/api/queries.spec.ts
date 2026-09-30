import { describe, expect, it } from 'vitest'

import { eventKeys, eventList, eventPages, eventQueries } from '@/entities/event'
import { FEATURED_FIRST_SORT } from '@/shared/api'

import { apiError, http, HttpResponse, paged, server } from '../../../../support/server'
import {
  buildEventListItem,
  buildEventResponse,
  buildLeaderRosterActivity,
  buildTermsDocumentState,
} from '../../../../support/builders'
import { queryOf } from '../../../../support/dom'
import { createTestQueryClient } from '../../../../support/render'

describe('eventKeys', () => {
  it('nests every public event query under the events root', () => {
    expect(eventKeys.all).toEqual(['events'])
    expect(eventKeys.upcoming()).toEqual(['events', 'upcoming'])
    expect(eventKeys.past({ year: '2025', search: 'robot', categoryId: 'cat-1' })).toEqual([
      'events',
      'past',
      '2025',
      'robot',
      'cat-1',
    ])
    expect(eventKeys.pastYears()).toEqual(['events', 'past-years'])
    expect(eventKeys.pastCategories()).toEqual(['events', 'past-categories'])
    expect(eventKeys.home()).toEqual(['events', 'board'])
    expect(eventKeys.detail('e1')).toEqual(['events', 'detail', 'e1'])
    expect(eventKeys.terms('e1')).toEqual(['events', 'terms', 'e1'])
    expect(eventKeys.leaderRoster('e1', 'u1')).toEqual(['events', 'leader-roster', 'e1', 'u1'])
  })
})

describe('eventQueries', () => {
  it('loads a whole event, or null when it does not exist', async () => {
    server.use(
      http.get('/api/events/e1', () => HttpResponse.json(buildEventResponse({ id: 'e1' }))),
      http.get('/api/events/missing', () => apiError(404)),
    )
    const client = createTestQueryClient()

    await expect(client.fetchQuery(eventQueries.detail('e1'))).resolves.toMatchObject({
      id: 'e1',
    })
    await expect(client.fetchQuery(eventQueries.detail('missing'))).resolves.toBeNull()
    expect(eventQueries.detail('e1').queryKey).toEqual(eventKeys.detail('e1'))
  })

  it('loads the home board and the filter options of past events', async () => {
    server.use(
      http.get('/api/events', ({ request }) =>
        queryOf(request.url).sort === FEATURED_FIRST_SORT
          ? HttpResponse.json(paged([buildEventListItem({ id: 'e1' })]))
          : HttpResponse.json(
              paged([buildEventListItem({ id: 'e1' }), buildEventListItem({ id: 'e2' })]),
            ),
      ),
      http.get('/api/events/past-years', () => HttpResponse.json([2025])),
      http.get('/api/events/past-categories', () =>
        HttpResponse.json([{ id: 'cat-1', name: 'IA', color: '#123456' }]),
      ),
    )
    const client = createTestQueryClient()

    const home = await client.fetchQuery(eventQueries.home())
    expect(home.featured?.id).toBe('e1')
    expect(home.items.map((event) => event.id)).toEqual(['e2'])
    await expect(client.fetchQuery(eventQueries.pastYears())).resolves.toEqual(['2025'])
    await expect(client.fetchQuery(eventQueries.pastCategories())).resolves.toEqual([
      { id: 'cat-1', name: 'IA', color: '#123456' },
    ])
  })

  it('loads the terms state and the roster of the activities a user leads', async () => {
    server.use(
      http.get('/api/events/e1/terms', () =>
        HttpResponse.json({ documents: [buildTermsDocumentState()], signupBlocked: true }),
      ),
      http.get('/api/events/e1/leader-roster', () =>
        HttpResponse.json([buildLeaderRosterActivity()]),
      ),
    )
    const client = createTestQueryClient()

    await expect(client.fetchQuery(eventQueries.terms('e1'))).resolves.toMatchObject({
      signupBlocked: true,
      documents: [{ id: 'terms-1' }],
    })
    await expect(client.fetchQuery(eventQueries.leaderRoster('e1', 'u1'))).resolves.toEqual([
      expect.objectContaining({ id: 'act-1' }),
    ])
    expect(eventQueries.leaderRoster('e1', 'u1').queryKey).toEqual(
      eventKeys.leaderRoster('e1', 'u1'),
    )
  })
})

describe('eventPages', () => {
  it('pages the upcoming events under their key', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/events', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildEventListItem()], 3))
      }),
    )
    const source = eventPages.upcoming()

    await expect(source.fetchPage(2, 10)).resolves.toMatchObject({ total: 3 })
    expect(source.queryKey).toEqual(eventKeys.upcoming())
    expect(queryOf(urls[0] ?? '')).toEqual({
      scope: 'Upcoming',
      sort: 'eventStartsAt',
      page: '2',
      pageSize: '10',
    })
  })

  it('pages the past events of the filters under a key of those filters', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/events', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildEventListItem()], 1))
      }),
    )
    const filters = { year: '2025', search: 'robot', categoryId: '' }
    const source = eventPages.past(filters)

    await expect(source.fetchPage(1, 25)).resolves.toMatchObject({
      items: [{ status: 'finished' }],
    })
    expect(source.queryKey).toEqual(eventKeys.past(filters))
    expect(queryOf(urls[0] ?? '')).toEqual({
      scope: 'Past',
      year: '2025',
      search: 'robot',
      sort: '-eventStartsAt',
      page: '1',
      pageSize: '25',
    })
  })
})

describe('eventList', () => {
  it('pages the admin table under the list key', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/events', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildEventListItem({ featured: true })], 1))
      }),
    )

    await expect(eventList.fetchPage({ title: 'hack', page: 1 })).resolves.toMatchObject({
      total: 1,
      items: [{ id: 'event-1', featured: true }],
    })
    expect(queryOf(urls[0] ?? '')).toEqual({ title: 'hack', page: '1' })
    expect(eventList.queryKey).toEqual(eventKeys.list())
    expect(eventKeys.list()).toEqual(['events', 'list'])
  })
})
