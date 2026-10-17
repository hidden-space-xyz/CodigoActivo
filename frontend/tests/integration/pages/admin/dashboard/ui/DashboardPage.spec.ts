import { beforeEach, describe, expect, it, vi } from 'vitest'

import { DashboardPage } from '@/pages/admin/dashboard'
import type { DashboardAnalyticsResponse } from '@/shared/api/generated/models'

import { renderWithProviders, t } from '../../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../../support/server'
import { buildDashboardAnalytics } from '../../../../../support/builders'
import { liveCharts, resetFakeCharts } from '../../../../../support/chart-mock'

vi.mock('chart.js', async () => (await import('../../../../../support/chart-mock')).chartJsModule)

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
  it('loads the last 12 months and renders KPIs, both sections and every chart', async () => {
    const ranges = serveAnalytics()

    const { wrapper } = await renderPage()
    await vi.waitFor(() => expect(wrapper.find('.kpi-grid').exists()).toBe(true))

    expect(ranges).toEqual([{ from: '2025-09-17', to: '2026-09-17' }])
    expect(wrapper.find('h1').text()).toBe(t('pages.admin.dashboard.header.title'))
    expect(wrapper.findAll('.kpi-card')).toHaveLength(8)
    expect(wrapper.findAll('.dashboard__section-title').map((title) => title.text())).toEqual([
      t('pages.admin.dashboard.sections.upcoming.title'),
      t('pages.admin.dashboard.sections.period.title'),
    ])
    expect(wrapper.text()).toContain(t('pages.admin.dashboard.pending.title'))
    expect(wrapper.text()).toContain(t('pages.admin.dashboard.roleCoverage.title'))
    expect(wrapper.text()).toContain(t('pages.admin.dashboard.occupancy.title'))
    expect(wrapper.findAll('.analytics-chart__empty')).toHaveLength(0)
    expect(liveCharts()).toHaveLength(7)
  })

  it('builds each chart from the matching part of the analytics', async () => {
    serveAnalytics()

    const { wrapper } = await renderPage()
    await vi.waitFor(() => expect(liveCharts()).toHaveLength(7))
    expect(liveCharts().every((chart) => chart.config.type === 'bar')).toBe(true)

    const newUsers = chartByTitle(t('pages.admin.dashboard.charts.newUsers.title'))
    expect(newUsers?.config.data).toMatchObject({
      datasets: [{ label: t('pages.admin.dashboard.series.member'), data: [1, 2] }],
    })

    const topEvents = chartByTitle(t('pages.admin.dashboard.charts.topEvents.title'))
    expect(topEvents?.config.data).toMatchObject({
      labels: [
        ['Hackathon de primavera', t('pages.admin.dashboard.chart.seatsCovered', { p: 75 })],
      ],
      datasets: [{ data: [42] }],
    })
    expect(topEvents?.config.options).toMatchObject({ indexAxis: 'y' })

    const categories = chartByTitle(t('pages.admin.dashboard.charts.categoryDemand.title'))
    expect(categories?.config.data).toMatchObject({
      labels: [['Programación']],
      datasets: [{ data: [3], backgroundColor: ['#ff6600'] }],
    })

    const profile = chartByTitle(t('pages.admin.dashboard.charts.participantProfile.title'))
    expect(profile?.config.data).toMatchObject({ datasets: [{ data: [0, 2, 1, 5] }] })

    const recurrence = chartByTitle(t('pages.admin.dashboard.charts.recurrence.title'))
    expect(recurrence?.config.data).toMatchObject({ datasets: [{ data: [6, 2] }] })

    const satisfaction = chartByTitle(t('pages.admin.dashboard.charts.satisfaction.title'))
    expect(satisfaction?.config.data).toMatchObject({ datasets: [{ data: [4.5] }] })
    const satisfactionCard = satisfaction?.canvas.closest('.chart-card')
    expect(satisfactionCard?.querySelector('.chart-card__subtitle')?.textContent).toBe(
      'Media 4,5 / 5 · 2 valoraciones con puntuación',
    )
    expect(wrapper.find('.pending__total').text()).toBe('3 solicitudes pendientes')
  })

  it('shows the empty placeholder for every chart without data', async () => {
    const noSeries = { buckets: [], series: [] }
    serveAnalytics(
      buildDashboardAnalytics({
        kpis: [],
        newUsers: noSeries,
        inscriptions: noSeries,
        topEvents: [],
        pendingSignups: { total: 0, events: [] },
        roleCoverage: [],
        participantProfile: { groups: ['adults'], series: [{ key: 'Male', values: [0] }] },
        categoryDemand: [{ key: 'cat-1', label: 'Programación', color: '#ff6600', count: 0 }],
        recurrence: [{ key: '1', count: 0 }],
        satisfaction: { scored: 0, events: [] },
        occupancy: { confirmed: 0, desired: 0, events: [] },
      }),
    )

    const { wrapper } = await renderPage()
    await vi.waitFor(() => expect(wrapper.find('.kpi-grid').exists()).toBe(true))

    expect(wrapper.findAll('.analytics-chart__empty')).toHaveLength(7)
    expect(liveCharts()).toHaveLength(0)
    expect(wrapper.text()).toContain(t('pages.admin.dashboard.charts.satisfaction.subtitle'))
    expect(wrapper.find('.pending__empty').exists()).toBe(true)
    expect(wrapper.find('.occupancy__empty').exists()).toBe(true)
    expect(wrapper.findAll('.kpi-card__value').map((value) => value.text())).toEqual(
      Array(8).fill('0'),
    )
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
