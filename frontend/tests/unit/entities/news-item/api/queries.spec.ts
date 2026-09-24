import { ref } from 'vue'
import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { useHomeNews, useNews, useNewsDetail } from '@/entities/news-item'
import type { NewsItemResponse, NewsListItemResponse } from '@/shared/api/generated/models'

import { renderComposable } from '../../../../support/fixtures/entities/composable'
import { apiError, http, HttpResponse, paged, server } from '../../../../support/server'

function item(id: string): NewsListItemResponse {
  return { id, title: `Novedad ${id}` }
}

describe('useNews', () => {
  it('selects the first year and loads its news items', async () => {
    const requested: Record<string, string>[] = []
    server.use(
      http.get('/api/news/years', () => HttpResponse.json([2026, 2025])),
      http.get('/api/news', ({ request }) => {
        requested.push(Object.fromEntries(new URL(request.url).searchParams))
        return HttpResponse.json(paged([item('a1')]))
      }),
    )

    const { result } = await renderComposable(() => useNews())
    await vi.waitFor(() => expect(result.news.value).toHaveLength(1))

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
      http.get('/api/news/years', () => HttpResponse.json([2026, 2025])),
      http.get('/api/news', ({ request }) => {
        const query = Object.fromEntries(new URL(request.url).searchParams)
        requested.push(query)
        return HttpResponse.json(paged([item(`${query.year}-${query.search ?? ''}`)]))
      }),
    )

    const { result } = await renderComposable(() => useNews())
    await vi.waitFor(() => expect(result.news.value).toHaveLength(1))

    result.setYear('2025')
    await vi.waitFor(() => expect(result.news.value[0]?.id).toBe('2025-'))

    result.setSearch('robot')
    await vi.waitFor(() => expect(result.news.value[0]?.id).toBe('2025-robot'))

    expect(result.search.value).toBe('robot')
    expect(requested.map((query) => [query.year, query.search])).toEqual([
      ['2026', undefined],
      ['2025', undefined],
      ['2025', 'robot'],
    ])
  })

  it('loads the next page on demand while more news items remain', async () => {
    const pages: string[] = []
    server.use(
      http.get('/api/news/years', () => HttpResponse.json([2026])),
      http.get('/api/news', ({ request }) => {
        const page = new URL(request.url).searchParams.get('page') ?? ''
        pages.push(page)
        return HttpResponse.json(paged([item(`p${page}`)], 2))
      }),
    )

    const { result } = await renderComposable(() => useNews())
    await vi.waitFor(() => expect(result.hasMore.value).toBe(true))

    result.loadMore()
    await vi.waitFor(() => expect(result.news.value).toHaveLength(2))

    expect(result.news.value.map((newsItem) => newsItem.id)).toEqual(['p1', 'p2'])
    expect(result.hasMore.value).toBe(false)
    expect(result.isFetchingMore.value).toBe(false)
    expect(pages).toEqual(['1', '2'])
  })

  it('does not request news items when there are no years', async () => {
    server.use(http.get('/api/news/years', () => HttpResponse.json([])))

    const { result } = await renderComposable(() => useNews())
    await flushPromises()

    expect(result.selectedYear.value).toBe('')
    expect(result.news.value).toEqual([])
    expect(result.isLoading.value).toBe(false)
  })

  it('keeps a selected year that is still available and resets one that disappears', async () => {
    let years = [2026, 2025]
    server.use(
      http.get('/api/news/years', () => HttpResponse.json(years)),
      http.get('/api/news', () => HttpResponse.json(paged([]))),
    )

    const { result, queryClient } = await renderComposable(() => useNews())
    await vi.waitFor(() => expect(result.selectedYear.value).toBe('2026'))
    result.setYear('2025')

    years = [2027, 2025]
    await queryClient.refetchQueries({ queryKey: ['news', 'years'] })
    await flushPromises()
    expect(result.selectedYear.value).toBe('2025')

    years = [2027]
    await queryClient.refetchQueries({ queryKey: ['news', 'years'] })
    await vi.waitFor(() => expect(result.selectedYear.value).toBe('2027'))
  })

  it('reports an error when the years cannot be loaded', async () => {
    server.use(http.get('/api/news/years', () => apiError(500)))

    const { result } = await renderComposable(() => useNews())

    await vi.waitFor(() => expect(result.isError.value).toBe(true))
    expect(result.isLoading.value).toBe(false)
  })

  it('reports an error when the year page fails', async () => {
    server.use(
      http.get('/api/news/years', () => HttpResponse.json([2026])),
      http.get('/api/news', () => apiError(500)),
    )

    const { result } = await renderComposable(() => useNews())

    await vi.waitFor(() => expect(result.isError.value).toBe(true))
  })
})

describe('useHomeNews', () => {
  it('starts empty and exposes the featured news item and the rest once loaded', async () => {
    server.use(
      http.get('/api/news', () => HttpResponse.json(paged([item('a1'), item('a2'), item('a3')]))),
    )

    const { result } = await renderComposable(() => {
      const home = useHomeNews()
      expect(home.featured.value).toBeNull()
      expect(home.items.value).toEqual([])
      return home
    })

    await vi.waitFor(() => expect(result.featured.value?.id).toBe('a1'))
    expect(result.items.value.map((newsItem) => newsItem.id)).toEqual(['a2', 'a3'])
    expect(result.isLoading.value).toBe(false)
    expect(result.isError.value).toBe(false)
  })
})

describe('useNewsDetail', () => {
  it('loads the news item for a reactive id', async () => {
    server.use(
      http.get('/api/news/:id', ({ params }) =>
        HttpResponse.json<NewsItemResponse>({
          id: String(params.id),
          title: `Novedad ${String(params.id)}`,
        }),
      ),
    )
    const id = ref('a1')

    const { result } = await renderComposable(() => useNewsDetail(id))
    await vi.waitFor(() => expect(result.newsItem.value?.title).toBe('Novedad a1'))
    expect(result.notFound.value).toBe(false)

    id.value = 'a2'
    await vi.waitFor(() => expect(result.newsItem.value?.title).toBe('Novedad a2'))
  })

  it('flags a missing news item as not found', async () => {
    server.use(http.get('/api/news/missing', () => apiError(404)))

    const { result } = await renderComposable(() => useNewsDetail('missing'))

    await vi.waitFor(() => expect(result.notFound.value).toBe(true))
    expect(result.isError.value).toBe(false)
  })
})
