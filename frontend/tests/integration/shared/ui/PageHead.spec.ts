import { describe, expect, it } from 'vitest'

import { PageHead } from '@/shared/ui'

import { renderWithProviders } from '../../../support/render'

describe('PageHead', () => {
  it('renders the eyebrow, title and intro slot', async () => {
    const { wrapper } = await renderWithProviders(PageHead, {
      props: { eyebrow: 'Agenda', title: 'Events' },
      slots: { default: '<p class="intro">Upcoming activities</p>' },
    })

    expect(wrapper.find('.eyebrow').text()).toBe('Agenda')
    expect(wrapper.find('h1').text()).toBe('Events')
    expect(wrapper.find('.intro').text()).toBe('Upcoming activities')
  })
})
