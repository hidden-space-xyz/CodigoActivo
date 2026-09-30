import { describe, expect, it } from 'vitest'

import { ChartCard } from '@/shared/ui/chart'

import { renderWithProviders } from '../../../../support/render'

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
