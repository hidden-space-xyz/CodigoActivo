import { describe, expect, it } from 'vitest'

import { DataState } from '@/shared/ui'

import { renderWithProviders, t } from '../../../support/render'

const slots = { default: '<ul class="content"><li>Row</li></ul>' }

describe('DataState', () => {
  it('shows a spinner while loading, even with an error', async () => {
    const { wrapper } = await renderWithProviders(DataState, {
      props: { loading: true, error: true, empty: true },
      slots,
    })

    expect(wrapper.text()).toBe(t('common.loading'))
    expect(wrapper.find('.app-icon--spin').exists()).toBe(true)
    expect(wrapper.find('.content').exists()).toBe(false)
  })

  it('shows the default or custom error message', async () => {
    const generic = await renderWithProviders(DataState, {
      props: { loading: false, error: true, empty: true },
      slots,
    })
    const custom = await renderWithProviders(DataState, {
      props: { loading: false, error: true, empty: false, errorText: 'Could not load events' },
      slots,
    })

    expect(generic.wrapper.find('.data-state--error').text()).toBe(t('dataState.error'))
    expect(custom.wrapper.text()).toBe('Could not load events')
  })

  it('shows the default or custom empty message', async () => {
    const generic = await renderWithProviders(DataState, {
      props: { loading: false, error: false, empty: true },
      slots,
    })
    const custom = await renderWithProviders(DataState, {
      props: { loading: false, error: false, empty: true, emptyText: 'No events yet' },
      slots,
    })

    expect(generic.wrapper.text()).toBe(t('dataState.empty'))
    expect(custom.wrapper.text()).toBe('No events yet')
  })

  it('renders the slot once data is available', async () => {
    const { wrapper } = await renderWithProviders(DataState, {
      props: { loading: false, error: false, empty: false },
      slots,
    })

    expect(wrapper.find('.content').text()).toBe('Row')
    expect(wrapper.find('.data-state').exists()).toBe(false)
  })
})
