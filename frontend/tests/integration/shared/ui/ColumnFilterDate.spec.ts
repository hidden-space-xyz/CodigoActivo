import { flushPromises, type VueWrapper } from '@vue/test-utils'
import { ElDatePicker, ElPopover } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import { ColumnFilterDate } from '@/shared/ui'

import { setMediaQueryMatches } from '../../../support/media'
import { renderWithProviders, t } from '../../../support/render'

type Range = (Date | null)[] | null

const from = new Date(2025, 0, 1)
const to = new Date(2025, 0, 31)

async function renderFilter(modelValue: Range) {
  const rendered = await renderWithProviders(ColumnFilterDate, {
    props: {
      modelValue,
      label: 'Created',
      'onUpdate:modelValue': (value: Range) => rendered.wrapper.setProps({ modelValue: value }),
    },
    attach: true,
  })
  return rendered
}

async function open(wrapper: VueWrapper): Promise<void> {
  await wrapper.find('.column-filter__toggle').trigger('click')
  await vi.waitFor(() => expect(popover(wrapper).props('visible')).toBe(true))
}

function popover(wrapper: VueWrapper) {
  return wrapper.findComponent(ElPopover)
}

function picker(wrapper: VueWrapper) {
  return wrapper.findComponent(ElDatePicker)
}

describe('ColumnFilterDate', () => {
  it('renders a linked two-panel range picker on wide screens', async () => {
    const { wrapper } = await renderFilter(null)

    expect(wrapper.find('.column-filter__toggle').attributes('aria-label')).toBe(
      t('table.filterBy', { label: 'Created' }),
    )
    const props = picker(wrapper).props()
    expect(props.type).toBe('daterange')
    expect(props.modelValue).toEqual([])
    expect(props.singlePanel).toBe(false)
    expect(props.unlinkPanels).toBe(true)
    expect(props.editable).toBe(false)
    expect(props.startPlaceholder).toBe(t('table.rangeFrom'))
    expect(props.endPlaceholder).toBe(t('table.rangeTo'))
  })

  it('uses a single editable panel on narrow screens', async () => {
    setMediaQueryMatches((query) => query === '(max-width: 640px)')

    const { wrapper } = await renderFilter(null)

    expect(picker(wrapper).props('singlePanel')).toBe(true)
    expect(picker(wrapper).props('editable')).toBe(true)
  })

  it('emits a complete range, applies it and closes the popover', async () => {
    const { wrapper } = await renderFilter(null)
    await open(wrapper)

    picker(wrapper).vm.$emit('update:modelValue', [from, to])
    await flushPromises()

    expect(wrapper.emitted('update:modelValue')).toEqual([[[from, to]]])
    expect(wrapper.emitted('apply')).toHaveLength(1)
    await vi.waitFor(() => expect(popover(wrapper).props('visible')).toBe(false))
    expect(wrapper.find('.column-filter__toggle').classes()).toContain(
      'column-filter__toggle--active',
    )
    expect(picker(wrapper).props('modelValue')).toEqual([from, to])
  })

  it('emits a half-selected range and keeps the popover open', async () => {
    const { wrapper } = await renderFilter(null)
    await open(wrapper)

    picker(wrapper).vm.$emit('update:modelValue', [from, 'not a date'])
    await flushPromises()

    expect(wrapper.emitted('update:modelValue')).toEqual([[[from]]])
    expect(popover(wrapper).props('visible')).toBe(true)
  })

  it('emits null when the picker is emptied', async () => {
    const { wrapper } = await renderFilter([from, to])

    picker(wrapper).vm.$emit('update:modelValue', null)
    await flushPromises()

    expect(wrapper.emitted('update:modelValue')).toEqual([[null]])
    expect(wrapper.emitted('apply')).toHaveLength(1)
  })

  it('clears the range from the clear button', async () => {
    const { wrapper } = await renderFilter([from, null])
    await open(wrapper)

    const clear = document.body.querySelector<HTMLButtonElement>('.column-filter__clear')
    expect(clear?.getAttribute('aria-label')).toBe(t('table.clearFilter'))
    clear?.click()
    await flushPromises()

    expect(wrapper.emitted('update:modelValue')).toEqual([[null]])
    await vi.waitFor(() => expect(popover(wrapper).props('visible')).toBe(false))
    expect(document.body.querySelector('.column-filter__clear')).toBeNull()
  })

  it('shows the clear button for a pending draft without active dates', async () => {
    const { wrapper } = await renderFilter([null, null])
    await open(wrapper)

    expect(wrapper.find('.column-filter__toggle').classes()).not.toContain(
      'column-filter__toggle--active',
    )
    expect(document.body.querySelector('.column-filter__clear')).not.toBeNull()

    await wrapper.setProps({ modelValue: null })
    await flushPromises()

    expect(document.body.querySelector('.column-filter__clear')).toBeNull()
  })
})
