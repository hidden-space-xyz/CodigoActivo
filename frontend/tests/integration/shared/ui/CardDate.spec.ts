import { describe, expect, it } from 'vitest'

import { CardDate } from '@/shared/ui'

import { renderWithProviders } from '../../../support/render'

describe('CardDate', () => {
  it('renders the label with a colon and the formatted value', async () => {
    const { wrapper } = await renderWithProviders(CardDate, {
      props: { label: 'Inicio', value: '15 ene 2025' },
    })

    expect(wrapper.find('time').exists()).toBe(true)
    expect(wrapper.find('.card-date__label').text()).toBe('Inicio:')
    expect(wrapper.find('.card-date__value').text()).toBe('15 ene 2025')
    expect(wrapper.find('.app-icon').exists()).toBe(true)
  })

  it('renders nothing without a value', async () => {
    const { wrapper } = await renderWithProviders(CardDate, {
      props: { label: 'Inicio', value: '' },
    })

    expect(wrapper.find('time').exists()).toBe(false)
  })
})
