import { describe, expect, it } from 'vitest'

import BlankLayout from '@/app/layouts/BlankLayout.vue'

import { renderWithProviders } from '../../../support/render'

describe('BlankLayout', () => {
  it('renders only the page content, without site chrome', async () => {
    const { wrapper } = await renderWithProviders(BlankLayout, {
      slots: { default: '<article class="printable">Roster</article>' },
    })

    expect(wrapper.find('.printable').text()).toBe('Roster')
    expect(wrapper.find('header').exists()).toBe(false)
    expect(wrapper.find('footer').exists()).toBe(false)
  })
})
