import { ref } from 'vue'
import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import {
  useEventDetail,
  useHomeEvents,
  usePastEventCategories,
  usePastEventsPaged,
  usePastEventYears,
  useUpcomingEventsPaged,
} from '@/entities/event'
import type { PastEventFilters } from '@/entities/event/model/types'
import { FEATURED_FIRST_SORT } from '@/shared/api'
import type { EventResponse } from '@/shared/api/generated/models'

import { renderComposable } from '../../../../support/fixtures/entities/composable'
import { apiError, http, HttpResponse, paged, server } from '../../../../support/server'

function queryOf(request: Request): Record<string, string> {
  return Object.fromEntries(new URL(request.url).searchParams)
}

describe('useUpcomingEventsPaged', () => {
  it('loads upcoming events page by page', async () => {
    const pages: string[] = []
    server.use(
      http.get('/api/events', ({ request }) => {
        const query = queryOf(request)
        pages.push(query.page ?? '')
        return HttpResponse.json(paged([{ id: `e${query.page ?? ''}` }], 2))
      }),
    )

    const { result } = await renderComposable(() => useUpcomingEventsPaged())
    await vi.waitFor(() => expect(result.items.value).toHaveLength(1))
    expect(result.hasMore.value).toBe(true)
    expect(result.total.value).toBe(2)

    result.loadMore()
    await vi.waitFor(() =>
      expect(result.items.value.map((event) => event.id)).toEqual(['e1', 'e2']),
    )
    expect(result.hasMore.value).toBe(false)
    expect(pages).toEqual(['1', '2'])
  })
})

describe('usePastEventYears and usePastEventCategories', () => {
  it('start empty and expose the loaded filter options', async () => {
    server.use(
      http.get('/api/events/past-years', () => HttpResponse.json([2025])),
      http.get('/api/events/past-categories', () =>
        HttpResponse.json([{ id: 'cat-1', name: 'IA', color: '#123456' }]),
      ),
    )

    const { result } = await renderComposable(() => {
      const years = usePastEventYears()
      const categories = usePastEventCategories()
      expect(years.years.value).toEqual([])
      expect(categories.categories.value).toEqual([])
      return { years, categories }
    })

    await vi.waitFor(() => expect(result.years.years.value).toEqual(['2025']))
    await vi.waitFor(() =>
      expect(result.categories.categories.value).toEqual([
        { id: 'cat-1', name: 'IA', color: '#123456' },
      ]),
    )
    expect(result.years.isLoading.value).toBe(false)
    expect(result.categories.isError.value).toBe(false)
  })

  it('report errors', async () => {
    server.use(
      http.get('/api/events/past-years', () => apiError(500)),
      http.get('/api/events/past-categories', () => apiError(500)),
    )

    const { result } = await renderComposable(() => ({
      years: usePastEventYears(),
      categories: usePastEventCategories(),
    }))

    await vi.waitFor(() => expect(result.years.isError.value).toBe(true))
    await vi.waitFor(() => expect(result.categories.isError.value).toBe(true))
  })
})

describe('usePastEventsPaged', () => {
  it('waits for a year and reloads when the filters change', async () => {
    const queries: Record<string, string>[] = []
    server.use(
      http.get('/api/events', ({ request }) => {
        const query = queryOf(request)
        queries.push(query)
        return HttpResponse.json(paged([{ id: `${query.year}-${query.search ?? ''}` }]))
      }),
    )
    const filters = ref<PastEventFilters>({ year: '', search: '', categoryId: '' })

    const { result } = await renderComposable(() => usePastEventsPaged(filters))
    await flushPromises()
    expect(queries).toEqual([])
    expect(result.items.value).toEqual([])

    filters.value = { year: '2025', search: '', categoryId: '' }
    await vi.waitFor(() => expect(result.items.value[0]?.id).toBe('2025-'))

    filters.value = { year: '2025', search: 'robot', categoryId: 'cat-1' }
    await vi.waitFor(() => expect(result.items.value[0]?.id).toBe('2025-robot'))

    expect(queries.map((query) => [query.year, query.search, query.categoryTypeId])).toEqual([
      ['2025', undefined, undefined],
      ['2025', 'robot', 'cat-1'],
    ])
  })
})

describe('useHomeEvents', () => {
  it('exposes the featured event and the other upcoming events', async () => {
    server.use(
      http.get('/api/events', ({ request }) =>
        queryOf(request).sort === FEATURED_FIRST_SORT
          ? HttpResponse.json(paged([{ id: 'e1' }]))
          : HttpResponse.json(paged([{ id: 'e1' }, { id: 'e2' }])),
      ),
    )

    const { result } = await renderComposable(() => {
      const home = useHomeEvents()
      expect(home.featured.value).toBeNull()
      expect(home.items.value).toEqual([])
      return home
    })

    await vi.waitFor(() => expect(result.featured.value?.id).toBe('e1'))
    expect(result.items.value.map((event) => event.id)).toEqual(['e2'])
    expect(result.isLoading.value).toBe(false)
    expect(result.isError.value).toBe(false)
  })
})

describe('useEventDetail', () => {
  it('loads the event for a reactive id', async () => {
    server.use(
      http.get('/api/events/:id', ({ params }) =>
        HttpResponse.json<EventResponse>({ id: String(params.id), title: String(params.id) }),
      ),
    )
    const id = ref('e1')

    const { result } = await renderComposable(() => useEventDetail(id))
    await vi.waitFor(() => expect(result.event.value?.title).toBe('e1'))
    expect(result.notFound.value).toBe(false)

    id.value = 'e2'
    await vi.waitFor(() => expect(result.event.value?.title).toBe('e2'))
  })

  it('flags a missing event as not found', async () => {
    server.use(http.get('/api/events/missing', () => apiError(404)))

    const { result } = await renderComposable(() => useEventDetail(() => 'missing'))

    await vi.waitFor(() => expect(result.notFound.value).toBe(true))
    expect(result.isError.value).toBe(false)
  })
})
