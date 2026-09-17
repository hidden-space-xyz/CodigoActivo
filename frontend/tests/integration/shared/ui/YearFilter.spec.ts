import { describe, expect, it } from 'vitest'

import { YearFilter } from '@/shared/ui'

import { renderWithProviders, t } from '../../../support/render'

describe('YearFilter', () => {
  it('renders one pill per year and marks the selected one', async () => {
    const { wrapper } = await renderWithProviders(YearFilter, {
      props: { years: ['2025', '2024', '2023'], selected: '2024' },
    })

    expect(wrapper.attributes('role')).toBe('group')
    expect(wrapper.attributes('aria-label')).toBe(t('table.filterByYear'))
    const pills = wrapper.findAll('button')
    expect(pills.map((pill) => pill.text())).toEqual(['2025', '2024', '2023'])
    expect(pills.map((pill) => pill.attributes('aria-pressed'))).toEqual(['false', 'true', 'false'])
    expect(pills[1]?.classes()).toContain('year-filter__pill--active')
    expect(pills[0]?.attributes('title')).toBe(t('table.filterByYearValue', { year: '2025' }))
  })

  it('emits the clicked year, including the selected one', async () => {
    const { wrapper } = await renderWithProviders(YearFilter, {
      props: { years: ['2025', '2024'], selected: '2025' },
    })

    await wrapper.findAll('button')[1]?.trigger('click')
    await wrapper.findAll('button')[0]?.trigger('click')

    expect(wrapper.emitted('select')).toEqual([['2024'], ['2025']])
  })

  it('leaves every pill unpressed when the selection is not listed', async () => {
    const { wrapper } = await renderWithProviders(YearFilter, {
      props: { years: ['2025'], selected: '1999' },
    })

    expect(wrapper.find('button').attributes('aria-pressed')).toBe('false')
  })
})
