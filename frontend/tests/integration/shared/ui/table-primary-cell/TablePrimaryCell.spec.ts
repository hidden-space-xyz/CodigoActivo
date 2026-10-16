import { h } from 'vue'
import { describe, expect, it } from 'vitest'

import { TablePrimaryCell } from '@/shared/ui/table-primary-cell'

import { renderWithProviders } from '../../../../support/render'

describe('TablePrimaryCell', () => {
  it('renders the title and subtitle with their full text as tooltips', async () => {
    const { wrapper } = await renderWithProviders(TablePrimaryCell, {
      props: { title: 'Taller de robótica', subtitle: 'Para todas las edades' },
    })

    const title = wrapper.find('.table-primary-cell__title')
    expect(title.element.tagName).toBe('SPAN')
    expect(title.text()).toBe('Taller de robótica')
    expect(title.attributes('title')).toBe('Taller de robótica')

    const subtitle = wrapper.find('.table-primary-cell__subtitle')
    expect(subtitle.text()).toBe('Para todas las edades')
    expect(subtitle.attributes('title')).toBe('Para todas las edades')

    expect(wrapper.find('.table-primary-cell__leading').exists()).toBe(false)
    expect(wrapper.find('.table-primary-cell__badges').exists()).toBe(false)
    expect(wrapper.find('.table-primary-cell__meta').exists()).toBe(false)
  })

  it('omits an empty subtitle', async () => {
    const { wrapper } = await renderWithProviders(TablePrimaryCell, {
      props: { title: 'Ada Lovelace', subtitle: null },
    })

    expect(wrapper.find('.table-primary-cell__subtitle').exists()).toBe(false)
  })

  it('renders the leading, badges and meta slots', async () => {
    const { wrapper } = await renderWithProviders(TablePrimaryCell, {
      props: { title: 'Ada Lovelace' },
      slots: {
        leading: () => h('img', { class: 'thumb', alt: '' }),
        badges: () => h('span', { class: 'badge' }, 'Admin'),
        meta: () => h('button', { type: 'button' }, '2 dependientes'),
      },
    })

    expect(wrapper.find('.table-primary-cell__leading .thumb').exists()).toBe(true)
    expect(wrapper.find('.table-primary-cell__head .table-primary-cell__badges').text()).toBe(
      'Admin',
    )
    expect(wrapper.find('.table-primary-cell__meta button').text()).toBe('2 dependientes')
  })
})
