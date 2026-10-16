import { describe, expect, it } from 'vitest'

import FilterChip from '@/shared/ui/data-table/FilterChip.vue'

import { renderWithProviders } from '../../../../support/render'

describe('FilterChip', () => {
  it('shows its text and emits remove from a labelled button', async () => {
    const { wrapper } = await renderWithProviders(FilterChip, {
      props: { label: 'Estado: Activo', removeLabel: 'Quitar el filtro «Estado: Activo»' },
    })

    expect(wrapper.element.tagName).toBe('LI')
    const label = wrapper.find('.filter-chip__label')
    expect(label.text()).toBe('Estado: Activo')
    expect(label.attributes('title')).toBe('Estado: Activo')

    const remove = wrapper.find('button.filter-chip__remove')
    expect(remove.attributes('type')).toBe('button')
    expect(remove.attributes('aria-label')).toBe('Quitar el filtro «Estado: Activo»')
    expect(remove.find('.app-icon').exists()).toBe(true)

    await remove.trigger('click')

    expect(wrapper.emitted('remove')).toHaveLength(1)
  })
})
