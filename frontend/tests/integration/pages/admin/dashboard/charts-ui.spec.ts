import { nextTick } from 'vue'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import AnalyticsChart from '@/pages/admin/dashboard/ui/AnalyticsChart.vue'
import BaseChart from '@/pages/admin/dashboard/ui/BaseChart.vue'
import ChartCard from '@/pages/admin/dashboard/ui/ChartCard.vue'

import {
  fakeCharts,
  liveCharts,
  registeredPlugins,
  resetFakeCharts,
} from '../../../../support/fixtures/public-dashboard/chart-mock'
import { renderWithProviders, t } from '../../../../support/render'

vi.mock(
  'chart.js',
  async () =>
    (await import('../../../../support/fixtures/public-dashboard/chart-mock')).chartJsModule,
)

beforeEach(() => {
  resetFakeCharts()
})

describe('BaseChart', () => {
  const data = { labels: ['a'], datasets: [{ data: [1] }] }
  const options = { responsive: true }

  it('registers the Chart.js components and draws on its canvas when mounted', async () => {
    const { wrapper } = await renderWithProviders(BaseChart, {
      props: { type: 'bar', data, options },
    })

    expect(registeredPlugins).toContainEqual(['fake-registerable'])
    expect(fakeCharts).toHaveLength(1)
    expect(fakeCharts[0]?.canvas).toBe(wrapper.find('canvas').element)
    expect(fakeCharts[0]?.config).toEqual({ type: 'bar', data, options })
  })

  it('recreates the chart when the type changes', async () => {
    const { wrapper } = await renderWithProviders(BaseChart, {
      props: { type: 'bar', data, options },
    })

    await wrapper.setProps({ type: 'line' })

    expect(fakeCharts).toHaveLength(2)
    expect(fakeCharts[0]?.destroyed).toBe(true)
    expect(liveCharts().map((chart) => chart.config.type)).toEqual(['line'])
  })

  it('recreates the chart when the data is replaced or mutated deeply', async () => {
    const mutable = { labels: ['a'], datasets: [{ data: [1] }] }
    const { wrapper } = await renderWithProviders(BaseChart, {
      props: { type: 'bar', data: mutable, options },
    })

    await wrapper.setProps({ data: { labels: ['b'], datasets: [] } })
    expect(fakeCharts).toHaveLength(2)

    const props = wrapper.props() as { data: { labels: string[] } }
    props.data.labels.push('c')
    await nextTick()

    expect(fakeCharts).toHaveLength(3)
    expect(liveCharts()).toHaveLength(1)
    expect(liveCharts()[0]?.config.data).toEqual({ labels: ['b', 'c'], datasets: [] })
  })

  it('recreates the chart when the options change', async () => {
    const { wrapper } = await renderWithProviders(BaseChart, {
      props: { type: 'doughnut', data, options },
    })

    await wrapper.setProps({ options: { responsive: false } })

    expect(fakeCharts).toHaveLength(2)
    expect(liveCharts()[0]?.config.options).toEqual({ responsive: false })
  })

  it('destroys the chart when unmounted', async () => {
    const { wrapper } = await renderWithProviders(BaseChart, {
      props: { type: 'bar', data, options },
    })

    wrapper.unmount()

    expect(fakeCharts[0]?.destroyed).toBe(true)
  })
})

describe('ChartCard', () => {
  it('renders the heading, the optional subtitle and the slot', async () => {
    const { wrapper } = await renderWithProviders(ChartCard, {
      props: { title: 'Usuarios', subtitle: 'Por tipo' },
      slots: { default: '<p class="slot-body">contenido</p>' },
    })

    expect(wrapper.find('h2').text()).toBe('Usuarios')
    expect(wrapper.find('.chart-card__subtitle').text()).toBe('Por tipo')
    expect(wrapper.find('.slot-body').exists()).toBe(true)
  })

  it('omits the subtitle when it is missing', async () => {
    const { wrapper } = await renderWithProviders(ChartCard, { props: { title: 'Usuarios' } })

    expect(wrapper.find('.chart-card__subtitle').exists()).toBe(false)
  })
})

describe('AnalyticsChart', () => {
  const baseProps = {
    title: 'Inscripciones',
    subtitle: 'Por estado',
    type: 'bar',
    data: { labels: [], datasets: [] },
    options: {},
  }

  it('draws the chart inside a card with the default height', async () => {
    const { wrapper } = await renderWithProviders(AnalyticsChart, { props: baseProps })

    expect(wrapper.find('h2').text()).toBe('Inscripciones')
    expect(wrapper.find('.analytics-chart__canvas').attributes('style')).toContain('height: 260px')
    expect(wrapper.findComponent(BaseChart).exists()).toBe(true)
    expect(fakeCharts).toHaveLength(1)
  })

  it('shows the empty placeholder with the given height instead of a chart', async () => {
    const { wrapper } = await renderWithProviders(AnalyticsChart, {
      props: { ...baseProps, empty: true, height: 180 },
    })

    const empty = wrapper.find('.analytics-chart__empty')
    expect(empty.text()).toBe(t('pages.admin.dashboard.chart.empty'))
    expect(empty.attributes('style')).toContain('height: 180px')
    expect(wrapper.find('canvas').exists()).toBe(false)
    expect(fakeCharts).toHaveLength(0)
  })
})
