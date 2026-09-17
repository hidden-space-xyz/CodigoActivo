import { flushPromises } from '@vue/test-utils'
import { ElSelect } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import { EventsPage } from '@/pages/events'
import EventCategoryFilter from '@/pages/events/ui/EventCategoryFilter.vue'
import type {
  EventCategoryTypeResponse,
  EventListItemResponse,
} from '@/shared/api/generated/models'

import { buildEventListItem } from '../../../support/fixtures/public-dashboard/builders'
import { renderWithProviders, t } from '../../../support/render'
import { http, HttpResponse, paged, server } from '../../../support/server'

interface EventsApiOptions {
  upcoming?: EventListItemResponse[]
  upcomingTotal?: number
  past?: (params: URLSearchParams) => { items: EventListItemResponse[]; total?: number }
  years?: number[]
  categories?: EventCategoryTypeResponse[]
}

function serveEventsApi(options: EventsApiOptions = {}) {
  const upcomingPages: string[] = []
  const pastQueries: URLSearchParams[] = []
  server.use(
    http.get('/api/events', ({ request }) => {
      const params = new URL(request.url).searchParams
      if (params.get('scope') === 'Upcoming') {
        upcomingPages.push(params.get('page') ?? '')
        const page = Number(params.get('page'))
        const items = options.upcoming ?? []
        return HttpResponse.json(
          paged(
            page === 1 ? items : [buildEventListItem({ id: 'up-more', title: 'Más' })],
            options.upcomingTotal ?? items.length,
          ),
        )
      }
      pastQueries.push(params)
      const past = options.past?.(params) ?? { items: [] }
      return HttpResponse.json(paged(past.items, past.total ?? past.items.length))
    }),
    http.get('/api/events/past-years', () => HttpResponse.json(options.years ?? [2025, 2024])),
    http.get('/api/events/past-categories', () =>
      HttpResponse.json(
        options.categories ?? [
          { id: 'cat-1', name: 'Programación', color: '#ff6600' },
          { id: 'cat-2', name: 'Robótica', color: 'not-a-color' },
          { name: 'Sin id' },
        ],
      ),
    ),
  )
  return { upcomingPages, pastQueries }
}

function pastEvent(id: string, title: string): EventListItemResponse {
  return buildEventListItem({
    id,
    title,
    eventStartsAt: '2024-05-01T09:00:00Z',
    eventEndsAt: '2024-05-02T09:00:00Z',
  })
}

async function renderPage() {
  const rendered = await renderWithProviders(EventsPage, { route: '/events', attach: true })
  await vi.waitFor(() =>
    expect(rendered.wrapper.findAll('.events-loading').map((p) => p.text())).not.toContain(
      t('common.loading'),
    ),
  )
  return rendered
}

describe('events page', () => {
  it('lists upcoming events and the past events of the most recent year', async () => {
    const { pastQueries } = serveEventsApi({
      upcoming: [buildEventListItem({ id: 'up-1', title: 'Hackathon' })],
      past: () => ({ items: [pastEvent('past-1', 'Campamento 2025')] }),
    })

    const { wrapper } = await renderPage()

    expect(wrapper.find('h1').text()).toBe(t('pages.events.title'))
    expect(wrapper.find('.board').text()).toContain('Hackathon')
    expect(wrapper.find('.events-grid').text()).toContain('Campamento 2025')
    expect(wrapper.find('.year-filter__pill--active').text()).toBe('2025')
    expect(Object.fromEntries(pastQueries[0] ?? [])).toEqual({
      scope: 'Past',
      year: '2025',
      sort: '-eventStartsAt',
      page: '1',
      pageSize: '25',
    })
    expect(wrapper.find('.events-more').exists()).toBe(false)
  })

  it('shows loading messages until the lists arrive', async () => {
    server.use(
      http.get('/api/events', () => new Promise<never>(() => undefined)),
      http.get('/api/events/past-years', () => new Promise<never>(() => undefined)),
      http.get('/api/events/past-categories', () => HttpResponse.json([])),
    )

    const { wrapper } = await renderWithProviders(EventsPage, { route: '/events' })

    expect(wrapper.findAll('.events-loading').map((p) => p.text())).toEqual([
      t('common.loading'),
      t('common.loading'),
    ])
  })

  it('loads more upcoming events on demand', async () => {
    const { upcomingPages } = serveEventsApi({
      upcoming: [buildEventListItem({ id: 'up-1', title: 'Hackathon' })],
      upcomingTotal: 2,
    })

    const { wrapper } = await renderPage()
    const more = wrapper.find('.events-section .events-more button')
    await more.trigger('click')

    await vi.waitFor(() => expect(wrapper.find('.board').text()).toContain('Más'))
    expect(upcomingPages).toEqual(['1', '2'])
    expect(wrapper.findAll('.events-more')).toHaveLength(0)
  })

  it('loads more past events on demand', async () => {
    const { pastQueries } = serveEventsApi({
      past: (params) =>
        params.get('page') === '1'
          ? { items: [pastEvent('past-1', 'Primero')], total: 2 }
          : { items: [pastEvent('past-2', 'Segundo')], total: 2 },
    })

    const { wrapper } = await renderPage()
    await wrapper.find('.events-section--past .events-more button').trigger('click')

    await vi.waitFor(() => expect(wrapper.find('.events-grid').text()).toContain('Segundo'))
    expect(pastQueries.map((params) => params.get('page'))).toEqual(['1', '2'])
  })

  it('filters past events by year', async () => {
    const { pastQueries } = serveEventsApi({
      past: (params) => ({
        items: [pastEvent(`p-${params.get('year')}`, `Evento ${params.get('year')}`)],
      }),
    })

    const { wrapper } = await renderPage()
    const pill2024 = wrapper.findAll('.year-filter__pill').find((pill) => pill.text() === '2024')
    await pill2024?.trigger('click')

    await vi.waitFor(() => expect(wrapper.find('.events-grid').text()).toContain('Evento 2024'))
    expect(pastQueries.map((params) => params.get('year'))).toEqual(['2025', '2024'])
  })

  it('searches past events and reports when nothing matches', async () => {
    const { pastQueries } = serveEventsApi({
      past: (params) =>
        params.get('search') ? { items: [] } : { items: [pastEvent('past-1', 'Primero')] },
    })

    const { wrapper } = await renderPage()
    const input = wrapper.find('.events-filters__search input')
    await input.setValue('  robots ')
    await input.trigger('keydown', { key: 'Enter' })

    await vi.waitFor(() =>
      expect(wrapper.find('.events-section--past .events-loading').text()).toBe(
        t('pages.events.noResults'),
      ),
    )
    expect(pastQueries.at(-1)?.get('search')).toBe('robots')
  })

  it('filters past events by category and clears the filter', async () => {
    const { pastQueries } = serveEventsApi({
      past: () => ({ items: [pastEvent('past-1', 'Primero')] }),
    })

    const { wrapper } = await renderPage()
    const filter = wrapper.findComponent(EventCategoryFilter)
    expect(filter.props('categories')).toEqual([
      { id: 'cat-1', name: 'Programación', color: '#ff6600' },
      { id: 'cat-2', name: 'Robótica', color: 'not-a-color' },
    ])

    filter.findComponent(ElSelect).vm.$emit('change', 'cat-2')
    await vi.waitFor(() => expect(pastQueries.at(-1)?.get('categoryTypeId')).toBe('cat-2'))

    filter.findComponent(ElSelect).vm.$emit('change', undefined)
    await flushPromises()
    expect(filter.props('modelValue')).toBe('')
  })

  it('renders each category option with its color swatch', async () => {
    serveEventsApi()

    await renderPage()

    const options = [...document.body.querySelectorAll<HTMLElement>('.category-filter__option')]
    expect(options.map((option) => option.textContent?.trim())).toEqual([
      'Programación',
      'Robótica',
    ])
    const swatches = options.map(
      (option) =>
        option.querySelector<HTMLElement>('.category-filter__swatch')?.style.backgroundColor,
    )
    expect(swatches[0]).not.toBe('')
    expect(swatches[1]).toBe('')
  })

  it('hides the filters and past list when there are no past years or categories', async () => {
    const { pastQueries } = serveEventsApi({ years: [], categories: [] })

    const { wrapper } = await renderPage()
    await flushPromises()

    expect(wrapper.find('.events-filters').exists()).toBe(false)
    expect(wrapper.find('.events-section--past .events-loading').exists()).toBe(false)
    expect(pastQueries).toEqual([])
  })

  it('hides the category filter when no category exists', async () => {
    serveEventsApi({ categories: [] })

    const { wrapper } = await renderPage()

    expect(wrapper.find('.events-filters').exists()).toBe(true)
    expect(wrapper.findComponent(EventCategoryFilter).exists()).toBe(false)
  })
})
