import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { newsKeys } from '@/entities/news-item'
import { useNewsArchive } from '@/pages/news/model/use-news-archive'

import { http, HttpResponse, paged, server } from '../../../../support/server'
import { buildNewsListItem } from '../../../../support/builders'
import { queryOf } from '../../../../support/dom'
import { mountComposable } from '../../../../support/render'

describe('useNewsArchive', () => {
  it('selects the newest year and loads its news items', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/news/years', () => HttpResponse.json([2026, 2025])),
      http.get('/api/news', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildNewsListItem({ id: 'a1' })]))
      }),
    )

    const { result } = await mountComposable(() => useNewsArchive())
    await vi.waitFor(() => expect(result.news.value).toHaveLength(1))

    expect(result.years.value).toEqual(['2026', '2025'])
    expect(result.selectedYear.value).toBe('2026')
    expect(result.isLoading.value).toBe(false)
    expect(result.hasMore.value).toBe(false)
    expect(urls.map(queryOf)).toEqual([
      { year: '2026', sort: '-createdAt', page: '1', pageSize: '25' },
    ])
  })

  it('reloads when the year or the search change', async () => {
    const queries: Record<string, string>[] = []
    server.use(
      http.get('/api/news/years', () => HttpResponse.json([2026, 2025])),
      http.get('/api/news', ({ request }) => {
        const query = queryOf(request.url)
        queries.push(query)
        return HttpResponse.json(
          paged([buildNewsListItem({ id: `${query.year ?? ''}-${query.search ?? ''}` })]),
        )
      }),
    )

    const { result } = await mountComposable(() => useNewsArchive())
    await vi.waitFor(() => expect(result.news.value).toHaveLength(1))

    result.setYear('2025')
    await vi.waitFor(() => expect(result.news.value[0]?.id).toBe('2025-'))

    result.setSearch('robot')
    await vi.waitFor(() => expect(result.news.value[0]?.id).toBe('2025-robot'))

    expect(result.search.value).toBe('robot')
    expect(queries.map((query) => [query.year, query.search])).toEqual([
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
        const page = queryOf(request.url).page ?? ''
        pages.push(page)
        return HttpResponse.json(paged([buildNewsListItem({ id: `p${page}` })], 2))
      }),
    )

    const { result } = await mountComposable(() => useNewsArchive())
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

    const { result } = await mountComposable(() => useNewsArchive())
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

    const { result, queryClient } = await mountComposable(() => useNewsArchive())
    await vi.waitFor(() => expect(result.selectedYear.value).toBe('2026'))
    result.setYear('2025')

    years = [2027, 2025]
    await queryClient.refetchQueries({ queryKey: newsKeys.years() })
    await flushPromises()
    expect(result.selectedYear.value).toBe('2025')

    years = [2027]
    await queryClient.refetchQueries({ queryKey: newsKeys.years() })
    await vi.waitFor(() => expect(result.selectedYear.value).toBe('2027'))
  })
})
