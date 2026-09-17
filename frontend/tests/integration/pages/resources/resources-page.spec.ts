import { describe, expect, it, vi } from 'vitest'

import { ResourcesPage } from '@/pages/resources'
import { formatDate } from '@/shared/lib'

import { buildResourceListItem } from '../../../support/fixtures/public-dashboard/builders'
import { renderWithProviders, t } from '../../../support/render'
import { http, HttpResponse, paged, server } from '../../../support/server'

function serveResources(total?: number) {
  const queries: URLSearchParams[] = []
  server.use(
    http.get('/api/resources', ({ request }) => {
      const params = new URL(request.url).searchParams
      queries.push(params)
      if (params.get('search')) return HttpResponse.json(paged([]))
      const item =
        params.get('page') === '1'
          ? buildResourceListItem()
          : buildResourceListItem({
              id: 'resource-2',
              title: 'Enlace externo',
              url: 'https://example.test/guia',
            })
      return HttpResponse.json(paged([item], total ?? 1))
    }),
  )
  return queries
}

async function renderPage() {
  const rendered = await renderWithProviders(ResourcesPage, { route: '/resources' })
  await vi.waitFor(() => expect(rendered.wrapper.find('.resources-grid').exists()).toBe(true))
  return rendered
}

describe('resources page', () => {
  it('lists the newest resources linking to their detail page', async () => {
    const queries = serveResources()

    const { wrapper } = await renderPage()

    expect(wrapper.find('h1').text()).toBe(t('pages.resources.title'))
    const card = wrapper.find('.resource-card')
    expect(card.attributes('href')).toBe('/resources/resource-1')
    expect(card.text()).toContain('Guía de Python')
    expect(card.text()).toContain(formatDate('2026-03-02T10:00:00Z'))
    expect(Object.fromEntries(queries[0] ?? [])).toEqual({
      sort: '-createdAt',
      page: '1',
      pageSize: '25',
    })
    expect(wrapper.find('.resources-more').exists()).toBe(false)
  })

  it('shows a loading message until the resources arrive', async () => {
    server.use(http.get('/api/resources', () => new Promise<never>(() => undefined)))

    const { wrapper } = await renderWithProviders(ResourcesPage, { route: '/resources' })

    expect(wrapper.find('.resources-loading').text()).toBe(t('common.loading'))
  })

  it('loads more resources, linking external ones directly', async () => {
    const queries = serveResources(2)

    const { wrapper } = await renderPage()
    await wrapper.find('.resources-more button').trigger('click')

    await vi.waitFor(() => expect(wrapper.findAll('.resource-card')).toHaveLength(2))
    expect(wrapper.findAll('.resource-card')[1]?.attributes('href')).toBe(
      'https://example.test/guia',
    )
    expect(queries.map((params) => params.get('page'))).toEqual(['1', '2'])
    expect(wrapper.find('.resources-more').exists()).toBe(false)
  })

  it('searches resources and reports when nothing matches', async () => {
    const queries = serveResources()

    const { wrapper } = await renderPage()
    const input = wrapper.find('.resources-filters__search input')
    await input.setValue('kotlin')
    await input.trigger('keydown', { key: 'Enter' })

    await vi.waitFor(() =>
      expect(wrapper.find('.resources-loading').text()).toBe(t('pages.resources.noResults')),
    )
    expect(queries.at(-1)?.get('search')).toBe('kotlin')
  })
})
