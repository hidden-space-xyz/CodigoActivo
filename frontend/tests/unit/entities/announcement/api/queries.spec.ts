import { ref } from 'vue'
import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import {
  useAnnouncementDetail,
  useAnnouncements,
  useHomeAnnouncements,
} from '@/entities/announcement'
import type {
  AnnouncementListItemResponse,
  AnnouncementResponse,
} from '@/shared/api/generated/models'

import { renderComposable } from '../../../../support/fixtures/entities/composable'
import { apiError, http, HttpResponse, paged, server } from '../../../../support/server'

function item(id: string): AnnouncementListItemResponse {
  return { id, title: `Anuncio ${id}` }
}

describe('useAnnouncements', () => {
  it('selects the first year and loads its announcements', async () => {
    const requested: Record<string, string>[] = []
    server.use(
      http.get('/api/announcements/years', () => HttpResponse.json([2026, 2025])),
      http.get('/api/announcements', ({ request }) => {
        requested.push(Object.fromEntries(new URL(request.url).searchParams))
        return HttpResponse.json(paged([item('a1')]))
      }),
    )

    const { result } = await renderComposable(() => useAnnouncements())
    await vi.waitFor(() => expect(result.announcements.value).toHaveLength(1))

    expect(result.years.value).toEqual(['2026', '2025'])
    expect(result.selectedYear.value).toBe('2026')
    expect(result.isLoading.value).toBe(false)
    expect(result.isError.value).toBe(false)
    expect(result.hasMore.value).toBe(false)
    expect(requested).toEqual([{ year: '2026', sort: '-createdAt', page: '1', pageSize: '25' }])
  })

  it('reloads when the year or the search change', async () => {
    const requested: Record<string, string>[] = []
    server.use(
      http.get('/api/announcements/years', () => HttpResponse.json([2026, 2025])),
      http.get('/api/announcements', ({ request }) => {
        const query = Object.fromEntries(new URL(request.url).searchParams)
        requested.push(query)
        return HttpResponse.json(paged([item(`${query.year}-${query.search ?? ''}`)]))
      }),
    )

    const { result } = await renderComposable(() => useAnnouncements())
    await vi.waitFor(() => expect(result.announcements.value).toHaveLength(1))

    result.setYear('2025')
    await vi.waitFor(() => expect(result.announcements.value[0]?.id).toBe('2025-'))

    result.setSearch('robot')
    await vi.waitFor(() => expect(result.announcements.value[0]?.id).toBe('2025-robot'))

    expect(result.search.value).toBe('robot')
    expect(requested.map((query) => [query.year, query.search])).toEqual([
      ['2026', undefined],
      ['2025', undefined],
      ['2025', 'robot'],
    ])
  })

  it('loads the next page on demand while more announcements remain', async () => {
    const pages: string[] = []
    server.use(
      http.get('/api/announcements/years', () => HttpResponse.json([2026])),
      http.get('/api/announcements', ({ request }) => {
        const page = new URL(request.url).searchParams.get('page') ?? ''
        pages.push(page)
        return HttpResponse.json(paged([item(`p${page}`)], 2))
      }),
    )

    const { result } = await renderComposable(() => useAnnouncements())
    await vi.waitFor(() => expect(result.hasMore.value).toBe(true))

    result.loadMore()
    await vi.waitFor(() => expect(result.announcements.value).toHaveLength(2))

    expect(result.announcements.value.map((announcement) => announcement.id)).toEqual(['p1', 'p2'])
    expect(result.hasMore.value).toBe(false)
    expect(result.isFetchingMore.value).toBe(false)
    expect(pages).toEqual(['1', '2'])
  })

  it('does not request announcements when there are no years', async () => {
    server.use(http.get('/api/announcements/years', () => HttpResponse.json([])))

    const { result } = await renderComposable(() => useAnnouncements())
    await flushPromises()

    expect(result.selectedYear.value).toBe('')
    expect(result.announcements.value).toEqual([])
    expect(result.isLoading.value).toBe(false)
  })

  it('keeps a selected year that is still available and resets one that disappears', async () => {
    let years = [2026, 2025]
    server.use(
      http.get('/api/announcements/years', () => HttpResponse.json(years)),
      http.get('/api/announcements', () => HttpResponse.json(paged([]))),
    )

    const { result, queryClient } = await renderComposable(() => useAnnouncements())
    await vi.waitFor(() => expect(result.selectedYear.value).toBe('2026'))
    result.setYear('2025')

    years = [2027, 2025]
    await queryClient.refetchQueries({ queryKey: ['announcements', 'years'] })
    await flushPromises()
    expect(result.selectedYear.value).toBe('2025')

    years = [2027]
    await queryClient.refetchQueries({ queryKey: ['announcements', 'years'] })
    await vi.waitFor(() => expect(result.selectedYear.value).toBe('2027'))
  })

  it('reports an error when the years cannot be loaded', async () => {
    server.use(http.get('/api/announcements/years', () => apiError(500)))

    const { result } = await renderComposable(() => useAnnouncements())

    await vi.waitFor(() => expect(result.isError.value).toBe(true))
    expect(result.isLoading.value).toBe(false)
  })

  it('reports an error when the year page fails', async () => {
    server.use(
      http.get('/api/announcements/years', () => HttpResponse.json([2026])),
      http.get('/api/announcements', () => apiError(500)),
    )

    const { result } = await renderComposable(() => useAnnouncements())

    await vi.waitFor(() => expect(result.isError.value).toBe(true))
  })
})

describe('useHomeAnnouncements', () => {
  it('starts empty and exposes the featured announcement and the rest once loaded', async () => {
    server.use(
      http.get('/api/announcements', () =>
        HttpResponse.json(paged([item('a1'), item('a2'), item('a3')])),
      ),
    )

    const { result } = await renderComposable(() => {
      const home = useHomeAnnouncements()
      expect(home.featured.value).toBeNull()
      expect(home.items.value).toEqual([])
      return home
    })

    await vi.waitFor(() => expect(result.featured.value?.id).toBe('a1'))
    expect(result.items.value.map((announcement) => announcement.id)).toEqual(['a2', 'a3'])
    expect(result.isLoading.value).toBe(false)
    expect(result.isError.value).toBe(false)
  })
})

describe('useAnnouncementDetail', () => {
  it('loads the announcement for a reactive id', async () => {
    server.use(
      http.get('/api/announcements/:id', ({ params }) =>
        HttpResponse.json<AnnouncementResponse>({
          id: String(params.id),
          title: `Anuncio ${String(params.id)}`,
        }),
      ),
    )
    const id = ref('a1')

    const { result } = await renderComposable(() => useAnnouncementDetail(id))
    await vi.waitFor(() => expect(result.announcement.value?.title).toBe('Anuncio a1'))
    expect(result.notFound.value).toBe(false)

    id.value = 'a2'
    await vi.waitFor(() => expect(result.announcement.value?.title).toBe('Anuncio a2'))
  })

  it('flags a missing announcement as not found', async () => {
    server.use(http.get('/api/announcements/missing', () => apiError(404)))

    const { result } = await renderComposable(() => useAnnouncementDetail('missing'))

    await vi.waitFor(() => expect(result.notFound.value).toBe(true))
    expect(result.isError.value).toBe(false)
  })
})
