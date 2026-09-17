import { flushPromises, type VueWrapper } from '@vue/test-utils'
import { ElPopover, ElSelect } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import { ColumnFilterSelect } from '@/shared/ui'

import { renderWithProviders, t } from '../../../support/render'

const options = [
  { label: 'Active', value: 'active' },
  { label: 'Yes', value: true },
  { label: 'No', value: false },
]

async function renderFilter(modelValue: string | boolean | null) {
  const rendered = await renderWithProviders(ColumnFilterSelect, {
    props: {
      modelValue,
      label: 'Status',
      options,
      'onUpdate:modelValue': (value: string | boolean | null) =>
        rendered.wrapper.setProps({ modelValue: value }),
    },
    attach: true,
  })
  return rendered
}

async function open(wrapper: VueWrapper): Promise<void> {
  await wrapper.find('.column-filter__toggle').trigger('click')
  await vi.waitFor(() => expect(wrapper.findComponent(ElPopover).props('visible')).toBe(true))
}

function popoverVisible(wrapper: VueWrapper): unknown {
  return wrapper.findComponent(ElPopover).props('visible')
}

describe('ColumnFilterSelect', () => {
  it('labels the toggle and placeholder after the column', async () => {
    const { wrapper } = await renderFilter(null)

    const toggle = wrapper.find('.column-filter__toggle')
    expect(toggle.attributes('aria-label')).toBe(t('table.filterBy', { label: 'Status' }))
    expect(toggle.classes()).not.toContain('column-filter__toggle--active')
    expect(wrapper.findComponent(ElSelect).props('placeholder')).toBe(
      t('table.filterBy', { label: 'Status' }),
    )
  })

  it('emits the picked option, applies it and closes the popover', async () => {
    const { wrapper } = await renderFilter(null)
    await open(wrapper)

    document.body.querySelector<HTMLElement>('.el-select__wrapper')?.click()
    let item: HTMLElement | undefined
    await vi.waitFor(() => {
      item = [...document.body.querySelectorAll<HTMLElement>('.el-select-dropdown__item')].find(
        (candidate) => candidate.textContent?.trim() === 'Yes',
      )
      expect(item).toBeDefined()
    })
    item?.click()
    await flushPromises()

    expect(wrapper.emitted('update:modelValue')).toEqual([[true]])
    expect(wrapper.emitted('apply')).toHaveLength(1)
    await vi.waitFor(() => expect(popoverVisible(wrapper)).toBe(false))
    expect(wrapper.find('.column-filter__toggle').classes()).toContain(
      'column-filter__toggle--active',
    )
  })

  it('treats an emptied select as no filter', async () => {
    const { wrapper } = await renderFilter('active')

    wrapper.findComponent(ElSelect).vm.$emit('change', undefined)
    wrapper.findComponent(ElSelect).vm.$emit('change', '')
    await flushPromises()

    expect(wrapper.emitted('update:modelValue')).toEqual([[null], [null]])
    expect(wrapper.emitted('apply')).toHaveLength(2)
  })

  it('clears the filter from the clear button', async () => {
    const { wrapper } = await renderFilter('active')
    await open(wrapper)

    const clear = document.body.querySelector<HTMLButtonElement>('.column-filter__clear')
    expect(clear?.getAttribute('aria-label')).toBe(t('table.clearFilter'))
    clear?.click()
    await flushPromises()

    expect(wrapper.emitted('update:modelValue')).toEqual([[null]])
    expect(wrapper.emitted('apply')).toHaveLength(1)
    expect(document.body.querySelector('.column-filter__clear')).toBeNull()
    await vi.waitFor(() => expect(popoverVisible(wrapper)).toBe(false))
  })

  it('follows external model changes', async () => {
    const { wrapper } = await renderFilter(null)

    await wrapper.setProps({ modelValue: false })
    expect(wrapper.findComponent(ElSelect).props('modelValue')).toBe(false)
    expect(wrapper.find('.column-filter__toggle').classes()).toContain(
      'column-filter__toggle--active',
    )

    await wrapper.setProps({ modelValue: null })
    expect(wrapper.findComponent(ElSelect).props('modelValue')).toBeNull()
  })
})
