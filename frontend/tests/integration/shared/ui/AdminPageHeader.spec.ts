import { describe, expect, it } from 'vitest'

import { AdminPageHeader } from '@/shared/ui'

import { renderWithProviders } from '../../../support/render'

describe('AdminPageHeader', () => {
  it('renders the title, subtitle and actions slot', async () => {
    const { wrapper } = await renderWithProviders(AdminPageHeader, {
      props: { title: 'Events', subtitle: 'Manage all events' },
      slots: { actions: '<button type="button">New event</button>' },
    })

    expect(wrapper.find('h1').text()).toBe('Events')
    expect(wrapper.find('.page-header__subtitle').text()).toBe('Manage all events')
    expect(wrapper.find('.page-header__actions button').text()).toBe('New event')
  })

  it('omits the subtitle when it is empty', async () => {
    const { wrapper } = await renderWithProviders(AdminPageHeader, { props: { title: 'Users' } })

    expect(wrapper.find('.page-header__subtitle').exists()).toBe(false)
  })
})
