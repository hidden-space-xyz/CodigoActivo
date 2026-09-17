import { describe, expect, it, vi } from 'vitest'

import { AnnouncementsPage } from '@/pages/announcements'

import { buildAnnouncementListItem } from '../../../support/fixtures/public-dashboard/builders'
import { renderWithProviders, t } from '../../../support/render'
import { http, HttpResponse, paged, server } from '../../../support/server'

function serveAnnouncements(options: { years?: number[]; total?: number } = {}) {
  const queries: URLSearchParams[] = []
  server.use(
    http.get('/api/announcements/years', () => HttpResponse.json(options.years ?? [2026, 2025])),
    http.get('/api/announcements', ({ request }) => {
      const params = new URL(request.url).searchParams
      queries.push(params)
      if (params.get('search')) return HttpResponse.json(paged([]))
      const year = params.get('year') ?? ''
      const page = params.get('page') ?? ''
      return HttpResponse.json(
        paged(
          [
            buildAnnouncementListItem({
              id: `announcement-${year}-${page}`,
              title: `Anuncio ${year} página ${page}`,
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
  const rendered = await renderWithProviders(AnnouncementsPage, { route: '/announcements' })
  await vi.waitFor(() =>
    expect(
      rendered.wrapper.find('.announcements-loading').exists() &&
        rendered.wrapper.find('.announcements-loading').text() === t('common.loading'),
    ).toBe(false),
  )
  return rendered
}

describe('announcements page', () => {
  it('lists the announcements of the most recent year', async () => {
    const queries = serveAnnouncements()

    const { wrapper } = await renderPage()

    expect(wrapper.find('h1').text()).toBe(t('pages.announcements.title'))
    expect(wrapper.find('.year-filter__pill--active').text()).toBe('2026')
    const card = wrapper.find('.announcement-card')
    expect(card.text()).toContain('Anuncio 2026 página 1')
    expect(card.attributes('href')).toBe('/announcements/announcement-2026-1')
    expect(Object.fromEntries(queries[0] ?? [])).toEqual({
      year: '2026',
      sort: '-createdAt',
      page: '1',
      pageSize: '25',
    })
    expect(wrapper.find('.announcements-more').exists()).toBe(false)
  })

  it('shows a loading message until the years arrive', async () => {
    server.use(http.get('/api/announcements/years', () => new Promise<never>(() => undefined)))

    const { wrapper } = await renderWithProviders(AnnouncementsPage, { route: '/announcements' })

    expect(wrapper.find('.announcements-loading').text()).toBe(t('common.loading'))
  })

  it('switches year and loads more announcements', async () => {
    const queries = serveAnnouncements({ total: 2 })

    const { wrapper } = await renderPage()
    await wrapper
      .findAll('.year-filter__pill')
      .find((pill) => pill.text() === '2025')
      ?.trigger('click')
    await vi.waitFor(() => expect(wrapper.text()).toContain('Anuncio 2025 página 1'))

    await wrapper.find('.announcements-more button').trigger('click')

    await vi.waitFor(() => expect(wrapper.text()).toContain('Anuncio 2025 página 2'))
    expect(queries.map((params) => `${params.get('year')}/${params.get('page')}`)).toEqual([
      '2026/1',
      '2025/1',
      '2025/2',
    ])
  })

  it('reports when a search matches nothing', async () => {
    const queries = serveAnnouncements()

    const { wrapper } = await renderPage()
    const input = wrapper.find('.announcements-filters__search input')
    await input.setValue('verano')
    await input.trigger('keydown', { key: 'Enter' })

    await vi.waitFor(() =>
      expect(wrapper.find('.announcements-loading').text()).toBe(
        t('pages.announcements.noResults'),
      ),
    )
    expect(queries.at(-1)?.get('search')).toBe('verano')
  })

  it('says there are no announcements yet when no year exists', async () => {
    const queries = serveAnnouncements({ years: [] })

    const { wrapper } = await renderPage()

    expect(wrapper.find('.announcements-filters').exists()).toBe(false)
    expect(wrapper.find('.announcements-loading').text()).toBe(t('pages.announcements.empty'))
    expect(queries).toEqual([])
  })
})
