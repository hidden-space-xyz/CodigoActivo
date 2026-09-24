import { describe, expect, it, vi } from 'vitest'

import { NewsPage } from '@/pages/news'

import { buildNewsListItem } from '../../../support/fixtures/public-dashboard/builders'
import { renderWithProviders, t } from '../../../support/render'
import { http, HttpResponse, paged, server } from '../../../support/server'

function serveNews(options: { years?: number[]; total?: number } = {}) {
  const queries: URLSearchParams[] = []
  server.use(
    http.get('/api/news/years', () => HttpResponse.json(options.years ?? [2026, 2025])),
    http.get('/api/news', ({ request }) => {
      const params = new URL(request.url).searchParams
      queries.push(params)
      if (params.get('search')) return HttpResponse.json(paged([]))
      const year = params.get('year') ?? ''
      const page = params.get('page') ?? ''
      return HttpResponse.json(
        paged(
          [
            buildNewsListItem({
              id: `news-item-${year}-${page}`,
              title: `Novedad ${year} página ${page}`,
            }),
          ],
          options.total ?? 1,
        ),
      )
    }),
  )
  return queries
}

async function renderPage() {
  const rendered = await renderWithProviders(NewsPage, { route: '/news' })
  await vi.waitFor(() =>
    expect(
      rendered.wrapper.find('.news-loading').exists() &&
        rendered.wrapper.find('.news-loading').text() === t('common.loading'),
    ).toBe(false),
  )
  return rendered
}

describe('news page', () => {
  it('lists the news items of the most recent year', async () => {
    const queries = serveNews()

    const { wrapper } = await renderPage()

    expect(wrapper.find('h1').text()).toBe(t('pages.news.title'))
    expect(wrapper.find('.year-filter__pill--active').text()).toBe('2026')
    const card = wrapper.find('.news-card')
    expect(card.text()).toContain('Novedad 2026 página 1')
    expect(card.attributes('href')).toBe('/news/news-item-2026-1')
    expect(Object.fromEntries(queries[0] ?? [])).toEqual({
      year: '2026',
      sort: '-createdAt',
      page: '1',
      pageSize: '25',
    })
    expect(wrapper.find('.news-more').exists()).toBe(false)
  })

  it('shows a loading message until the years arrive', async () => {
    server.use(http.get('/api/news/years', () => new Promise<never>(() => undefined)))

    const { wrapper } = await renderWithProviders(NewsPage, { route: '/news' })

    expect(wrapper.find('.news-loading').text()).toBe(t('common.loading'))
  })

  it('switches year and loads more news items', async () => {
    const queries = serveNews({ total: 2 })

    const { wrapper } = await renderPage()
    await wrapper
      .findAll('.year-filter__pill')
      .find((pill) => pill.text() === '2025')
      ?.trigger('click')
    await vi.waitFor(() => expect(wrapper.text()).toContain('Novedad 2025 página 1'))

    await wrapper.find('.news-more button').trigger('click')

    await vi.waitFor(() => expect(wrapper.text()).toContain('Novedad 2025 página 2'))
    expect(queries.map((params) => `${params.get('year')}/${params.get('page')}`)).toEqual([
      '2026/1',
      '2025/1',
      '2025/2',
    ])
  })

  it('reports when a search matches nothing', async () => {
    const queries = serveNews()

    const { wrapper } = await renderPage()
    const input = wrapper.find('.news-filters__search input')
    await input.setValue('verano')
    await input.trigger('keydown', { key: 'Enter' })

    await vi.waitFor(() =>
      expect(wrapper.find('.news-loading').text()).toBe(t('pages.news.noResults')),
    )
    expect(queries.at(-1)?.get('search')).toBe('verano')
  })

  it('says there are no news items yet when no year exists', async () => {
    const queries = serveNews({ years: [] })

    const { wrapper } = await renderPage()

    expect(wrapper.find('.news-filters').exists()).toBe(false)
    expect(wrapper.find('.news-loading').text()).toBe(t('pages.news.empty'))
    expect(queries).toEqual([])
  })
})
