import { beforeEach, describe, expect, it, vi } from 'vitest'

import { DashboardPage } from '@/pages/admin/dashboard'
import type { DashboardAnalyticsResponse } from '@/shared/api/generated/models'

import { buildDashboardAnalytics } from '../../../../support/fixtures/public-dashboard/builders'
import {
  liveCharts,
  resetFakeCharts,
} from '../../../../support/fixtures/public-dashboard/chart-mock'
import { renderWithProviders, t } from '../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../support/server'

vi.mock(
  'chart.js',
  async () =>
    (await import('../../../../support/fixtures/public-dashboard/chart-mock')).chartJsModule,
)

function renderPage() {
  return renderWithProviders(DashboardPage, {
    route: '/admin/dashboard',
    user: { isAdmin: true },
  })
}

function serveAnalytics(body: DashboardAnalyticsResponse = buildDashboardAnalytics()) {
  const ranges: { from: string | null; to: string | null }[] = []
  server.use(
    http.get('/api/reports/dashboard/analytics', ({ request }) => {
      const params = new URL(request.url).searchParams
      ranges.push({ from: params.get('from'), to: params.get('to') })
      return HttpResponse.json(body)
    }),
  )
  return ranges
}

function chartByTitle(title: string) {
  return liveCharts().find((chart) => {
    const card = chart.canvas.closest('.chart-card')
    return card?.querySelector('h2')?.textContent === title
  })
}

beforeEach(() => {
  resetFakeCharts()
  vi.useFakeTimers({ toFake: ['Date'] })
  vi.setSystemTime(new Date(2026, 8, 17, 10, 0))
})

describe('dashboard page', () => {
  it('loads the last 12 months and renders KPIs, charts and occupancy', async () => {
    const ranges = serveAnalytics()

    const { wrapper } = await renderPage()
    await vi.waitFor(() => expect(wrapper.find('.kpi-grid').exists()).toBe(true))

    expect(ranges).toEqual([{ from: '2025-09-17', to: '2026-09-17' }])
    expect(wrapper.find('h1').text()).toBe(t('pages.admin.dashboard.header.title'))
    expect(wrapper.findAll('.kpi-card')).toHaveLength(6)
    expect(wrapper.text()).toContain(t('pages.admin.dashboard.occupancy.title'))
    expect(wrapper.findAll('.analytics-chart__empty')).toHaveLength(0)
    expect(liveCharts()).toHaveLength(9)
  })

  it('builds each chart from the matching part of the analytics', async () => {
    serveAnalytics()

    const { wrapper } = await renderPage()
    await vi.waitFor(() => expect(liveCharts()).toHaveLength(9))
    expect(wrapper.exists()).toBe(true)

    const growth = chartByTitle(t('pages.admin.dashboard.charts.userGrowth.title'))
    expect(growth?.config.type).toBe('line')
    expect(growth?.config.data).toMatchObject({
      datasets: [{ label: t('pages.admin.dashboard.series.member'), data: [1, 2] }],
    })

    const categories = chartByTitle(t('pages.admin.dashboard.charts.categories.title'))
    expect(categories?.config.type).toBe('doughnut')
    expect(categories?.config.data).toMatchObject({
      labels: ['Programación'],
      datasets: [{ data: [3], backgroundColor: ['#ff6600'] }],
    })

    const topEvents = chartByTitle(t('pages.admin.dashboard.charts.topEvents.title'))
    expect(topEvents?.config.data).toMatchObject({
      labels: [['Hackathon de primavera']],
      datasets: [{ data: [42] }],
    })
    expect(topEvents?.config.options).toMatchObject({ indexAxis: 'y' })

    const gender = chartByTitle(t('pages.admin.dashboard.charts.participantsByGender.title'))
    expect(gender?.config.data).toMatchObject({ datasets: [{ data: [5] }] })
  })

  it('shows the empty placeholder for every chart without data', async () => {
    serveAnalytics({
      granularity: null,
      kpis: null,
      topEvents: [{ eventId: 'event-1' }],
    })

    const { wrapper } = await renderPage()
    await vi.waitFor(() => expect(wrapper.find('.kpi-grid').exists()).toBe(true))

    expect(wrapper.findAll('.analytics-chart__empty')).toHaveLength(8)
    expect(liveCharts()).toHaveLength(1)
    expect(liveCharts()[0]?.config.data).toMatchObject({
      labels: [['']],
      datasets: [{ data: [0] }],
    })
    expect(wrapper.find('.occupancy__empty').exists()).toBe(true)
    expect(wrapper.findAll('.kpi-card__value').map((value) => value.text())).toEqual(
      Array(6).fill('0'),
    )
  })

  it('renders every chart as empty when the response omits the sections', async () => {
    serveAnalytics({})

    const { wrapper } = await renderPage()
    await vi.waitFor(() => expect(wrapper.find('.kpi-grid').exists()).toBe(true))

    expect(wrapper.findAll('.analytics-chart__empty')).toHaveLength(9)
  })

  it('shows the error message when the analytics cannot be loaded', async () => {
    server.use(http.get('/api/reports/dashboard/analytics', () => apiError(500)))

    const { wrapper } = await renderPage()

    await vi.waitFor(() => expect(wrapper.text()).toContain(t('pages.admin.dashboard.error')))
    expect(wrapper.find('.kpi-grid').exists()).toBe(false)
  })

  it('shows a loading state before the analytics arrive', async () => {
    server.use(
      http.get('/api/reports/dashboard/analytics', () => new Promise<never>(() => undefined)),
    )

    const { wrapper } = await renderPage()

    expect(wrapper.text()).toContain(t('common.loading'))
  })

  it('reloads the analytics for the selected preset while dimming the previous data', async () => {
    let release: (() => void) | undefined
    const ranges: (string | null)[] = []
    server.use(
      http.get('/api/reports/dashboard/analytics', async ({ request }) => {
        const from = new URL(request.url).searchParams.get('from')
        ranges.push(from)
        if (from === '2026-08-19') {
          await new Promise<void>((resolve) => {
            release = resolve
          })
        }
        return HttpResponse.json(buildDashboardAnalytics({ rangeStart: from ?? '' }))
      }),
    )

    const { wrapper } = await renderPage()
    await vi.waitFor(() => expect(wrapper.find('.kpi-grid').exists()).toBe(true))

    const thirtyDays = wrapper
      .findAll('.range-filter__pill')
      .find((pill) => pill.text() === t('pages.admin.dashboard.range.preset30d'))
    await thirtyDays?.trigger('click')

    await vi.waitFor(() => expect(release).toBeDefined())
    expect(wrapper.find('.dashboard').classes()).toContain('dashboard--refetching')
    expect(thirtyDays?.attributes('aria-pressed')).toBe('true')

    release?.()
    await vi.waitFor(() =>
      expect(wrapper.find('.dashboard').classes()).not.toContain('dashboard--refetching'),
    )
    expect(ranges).toEqual(['2025-09-17', '2026-08-19'])
  })
})
