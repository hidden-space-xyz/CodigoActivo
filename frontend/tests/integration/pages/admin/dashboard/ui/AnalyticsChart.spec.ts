import { beforeEach, describe, expect, it, vi } from 'vitest'

import AnalyticsChart from '@/pages/admin/dashboard/ui/AnalyticsChart.vue'
import { BaseChart } from '@/shared/ui/chart'

import { renderWithProviders, t } from '../../../../../support/render'
import { fakeCharts, resetFakeCharts } from '../../../../../support/chart-mock'

vi.mock('chart.js', async () => (await import('../../../../../support/chart-mock')).chartJsModule)

beforeEach(() => {
  resetFakeCharts()
})

describe('AnalyticsChart', () => {
  const chart = { data: { labels: [], datasets: [] }, options: {}, empty: false }
  const baseProps = { title: 'Inscripciones', subtitle: 'Por estado', chart }

  it('draws a bar chart inside a card with the default height', async () => {
    const { wrapper } = await renderWithProviders(AnalyticsChart, { props: baseProps })

    expect(wrapper.find('h2').text()).toBe('Inscripciones')
    expect(wrapper.find('.analytics-chart__canvas').attributes('style')).toContain('height: 260px')
    expect(wrapper.findComponent(BaseChart).exists()).toBe(true)
    expect(fakeCharts).toHaveLength(1)
    expect(fakeCharts[0]?.config.type).toBe('bar')
  })

  it('shows the empty placeholder with the given height instead of a chart', async () => {
    const { wrapper } = await renderWithProviders(AnalyticsChart, {
      props: { ...baseProps, chart: { ...chart, empty: true }, height: 180 },
    })

    const empty = wrapper.find('.analytics-chart__empty')
    expect(empty.text()).toBe(t('pages.admin.dashboard.chart.empty'))
    expect(empty.attributes('style')).toContain('height: 180px')
    expect(wrapper.find('canvas').exists()).toBe(false)
    expect(fakeCharts).toHaveLength(0)
  })
})
