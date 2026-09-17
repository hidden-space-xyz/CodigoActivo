import { ref } from 'vue'
import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import {
  useEventAttendeesTable,
  useEventBadges,
  useEventRatingsTable,
  useEventRoster,
  useEventSummary,
} from '@/features/manage-events'

import {
  buildAttendee,
  buildBadge,
  buildRating,
  buildRosterActivity,
  buildSummary,
  EVENT_ID,
  setupComposable,
} from '../../../support/fixtures/admin-events/builders'
import { http, HttpResponse, paged, server } from '../../../support/server'

describe('useEventSummary', () => {
  it('loads the report summary for the event', async () => {
    server.use(
      http.get('/api/reports/events/:eventId/summary', ({ params }) =>
        HttpResponse.json(buildSummary({ eventId: String(params.eventId), activitiesCount: 9 })),
      ),
    )

    const { result } = await setupComposable(() => useEventSummary(EVENT_ID))

    await vi.waitFor(() => expect(result.data.value?.activitiesCount).toBe(9))
    expect(result.data.value?.eventId).toBe(EVENT_ID)
  })
})

describe('useEventAttendeesTable', () => {
  it('does not fetch while inactive and sends trimmed filters once active', async () => {
    const urls: URL[] = []
    server.use(
      http.get('/api/reports/events/:eventId/attendees', ({ request }) => {
        urls.push(new URL(request.url))
        return HttpResponse.json(paged([buildAttendee()], 1))
      }),
    )
    const active = ref(false)

    const { result } = await setupComposable(() => useEventAttendeesTable(EVENT_ID, active))
    await flushPromises()
    expect(urls).toHaveLength(0)

    active.value = true
    await vi.waitFor(() => expect(result.table.items.value).toHaveLength(1))
    expect(urls[0]?.pathname).toBe(`/api/reports/events/${EVENT_ID}/attendees`)
    expect(urls[0]?.searchParams.get('sort')).toBe('firstName')
    expect(urls[0]?.searchParams.has('search')).toBe(false)

    result.search.value = '  ada  '
    result.userTypeId.value = 'type-1'
    result.gender.value = 'Female'
    result.activityId.value = 'act-1'
    result.roleTypeId.value = 'role-1'
    result.statusId.value = 'status-1'

    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('statusId')).toBe('status-1'))
    const last = urls.at(-1)
    expect(last?.searchParams.get('search')).toBe('ada')
    expect(last?.searchParams.get('userTypeId')).toBe('type-1')
    expect(last?.searchParams.get('gender')).toBe('Female')
    expect(last?.searchParams.get('activityId')).toBe('act-1')
    expect(last?.searchParams.get('roleTypeId')).toBe('role-1')
    expect(result.filterParams()).toEqual({
      search: 'ada',
      userTypeId: 'type-1',
      gender: 'Female',
      activityId: 'act-1',
      roleTypeId: 'role-1',
      statusId: 'status-1',
    })
  })

  it('fetches every page with the current filters and sort for exports', async () => {
    const pages: URL[] = []
    server.use(
      http.get('/api/reports/events/:eventId/attendees', ({ request }) => {
        const url = new URL(request.url)
        if (url.searchParams.get('pageSize') === '100') {
          pages.push(url)
          const page = Number(url.searchParams.get('page'))
          const items = Array.from({ length: page === 1 ? 100 : 20 }, (_, index) =>
            buildAttendee({ userId: `u-${page}-${index}` }),
          )
          return HttpResponse.json(paged(items, 120))
        }
        return HttpResponse.json(paged([], 0))
      }),
    )

    const { result } = await setupComposable(() => useEventAttendeesTable(EVENT_ID, true))
    result.search.value = 'lovelace'
    result.table.sortOrder.value = -1

    const all = await result.fetchAllAttendees()

    expect(all).toHaveLength(120)
    expect(pages.map((url) => url.searchParams.get('page'))).toEqual(['1', '2'])
    expect(pages[0]?.searchParams.get('search')).toBe('lovelace')
    expect(pages[0]?.searchParams.get('sort')).toBe('-firstName')
  })
})

describe('useEventRatingsTable', () => {
  it('lists ratings highest score first only while active', async () => {
    const urls: URL[] = []
    server.use(
      http.get('/api/events/:eventId/ratings', ({ request }) => {
        urls.push(new URL(request.url))
        return HttpResponse.json(paged([buildRating()], 1))
      }),
    )
    const active = ref(false)

    const { result } = await setupComposable(() => useEventRatingsTable(EVENT_ID, active))
    await flushPromises()
    expect(urls).toHaveLength(0)

    active.value = true
    await vi.waitFor(() => expect(result.table.total.value).toBe(1))
    expect(urls[0]?.pathname).toBe(`/api/events/${EVENT_ID}/ratings`)
    expect(urls[0]?.searchParams.get('sort')).toBe('-score')
  })
})

describe('printable report queries', () => {
  it('loads badges and roster for the event', async () => {
    server.use(
      http.get('/api/reports/events/:eventId/badges', () =>
        HttpResponse.json({ eventId: EVENT_ID, title: 'Hackathon', badges: [buildBadge()] }),
      ),
      http.get('/api/reports/events/:eventId/roster', () =>
        HttpResponse.json({
          eventId: EVENT_ID,
          title: 'Hackathon',
          activities: [buildRosterActivity()],
        }),
      ),
    )

    const { result } = await setupComposable(() => ({
      badges: useEventBadges(EVENT_ID),
      roster: useEventRoster(() => EVENT_ID),
    }))

    await vi.waitFor(() => expect(result.badges.data.value?.badges).toHaveLength(1))
    await vi.waitFor(() => expect(result.roster.data.value?.activities).toHaveLength(1))
  })
})
