import { nextTick } from 'vue'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { BaseChart } from '@/shared/ui/chart'

import { renderWithProviders } from '../../../../support/render'
import {
  fakeCharts,
  liveCharts,
  registeredPlugins,
  resetFakeCharts,
} from '../../../../support/chart-mock'

vi.mock('chart.js', async () => (await import('../../../../support/chart-mock')).chartJsModule)

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
