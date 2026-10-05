import { describe, expect, it } from 'vitest'

import { renderWithProviders } from '../../../../support/render'
import { StatusPanel } from '@/shared/ui/status-panel'

describe('StatusPanel', () => {
  it('announces pending work politely next to a spinner', async () => {
    const { wrapper } = await renderWithProviders(StatusPanel, {
      props: { state: 'pending', text: 'Working…' },
    })

    expect(wrapper.find('.app-icon--spin').exists()).toBe(true)
    expect(wrapper.get('.status-panel__text').attributes('aria-live')).toBe('polite')
    expect(wrapper.find('[role="alert"]').exists()).toBe(false)
    expect(wrapper.find('h2').exists()).toBe(false)
  })

  it('shows a finished outcome with its title, hint, content and actions', async () => {
    const { wrapper } = await renderWithProviders(StatusPanel, {
      props: { state: 'success', title: 'Done', text: 'All set.', hint: 'Nothing else to do.' },
      slots: { default: '<p class="extra">More</p>', actions: '<a href="/next">Next</a>' },
    })

    expect(wrapper.get('.status-panel__icon--success').text()).toBe('✓')
    expect(wrapper.get('h2').text()).toBe('Done')
    expect(wrapper.get('.status-panel__text').attributes('role')).toBeUndefined()
    expect(wrapper.get('.status-panel__hint').text()).toBe('Nothing else to do.')
    expect(wrapper.get('.status-panel__hint + .extra').text()).toBe('More')
    expect(wrapper.get('.status-panel__actions a').text()).toBe('Next')
  })

  it('reports a failure as an alert and leaves out what it was not given', async () => {
    const { wrapper } = await renderWithProviders(StatusPanel, {
      props: { state: 'error', text: 'It failed.' },
    })

    expect(wrapper.get('.status-panel__icon--error').text()).toBe('!')
    expect(wrapper.get('[role="alert"]').text()).toBe('It failed.')
    expect(wrapper.find('.status-panel__hint').exists()).toBe(false)
    expect(wrapper.find('.status-panel__actions').exists()).toBe(false)
  })
})
