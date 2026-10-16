import { defineComponent, h } from 'vue'
import { flushPromises, type VueWrapper } from '@vue/test-utils'
import { ElDatePicker, ElPopover } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import { setMediaQueryMatches } from '../../../../support/media'
import { renderWithProviders, t } from '../../../../support/render'
import { formatDate, formatDateRange } from '@/shared/lib/date'
import { AppIcon } from '@/shared/ui/app-icon'
import { ColumnFilterDate, provideTableFilters } from '@/shared/ui/column-filter'

type Range = (Date | null)[] | null

const from = new Date(2025, 0, 1)
const to = new Date(2025, 0, 31)

async function renderInTable(props: {
  modelValue: Range
  'onUpdate:modelValue'?: (value: Range) => void
}) {
  const box: { active?: ReturnType<typeof provideTableFilters>['active'] } = {}
  const Host = defineComponent({
    setup() {
      box.active = provideTableFilters().active
      return () => h(ColumnFilterDate, { label: 'Created', ...props })
    },
  })
  await renderWithProviders(Host, { attach: true })
  return () => box.active?.value ?? []
}

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
    expect(wrapper.find('.column-filter__toggle').findComponent(AppIcon).props('name')).toBe(
      'calendar',
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

  it('renders as a toolbar pill', async () => {
    const { wrapper } = await renderWithProviders(ColumnFilterDate, {
      props: { modelValue: null, label: 'Birth', variant: 'toolbar' },
      attach: true,
    })

    const pill = wrapper.find('.column-filter__pill')
    expect(pill.text()).toBe('Birth')
    expect(pill.findComponent(AppIcon).props('name')).toBe('calendar')
    expect(wrapper.find('.column-filter__toggle').exists()).toBe(false)
  })

  it.each([
    ['a complete range', [from, to], formatDateRange('2025-01-01', '2025-01-31')],
    ['a start date', [from, null], t('table.rangeSince', { date: formatDate('2025-01-01') })],
    ['a half-selected range', [from], t('table.rangeSince', { date: formatDate('2025-01-01') })],
    ['an end date', [null, to], t('table.rangeUntil', { date: formatDate('2025-01-31') })],
  ])('registers %s as a readable chip value', async (_case, modelValue, value) => {
    const active = await renderInTable({ modelValue })

    expect(active().map(({ label, value: text }) => ({ label, value: text }))).toEqual([
      { label: 'Created', value },
    ])
  })

  it('registers no chip without dates and clears from the chip', async () => {
    const inactive = await renderInTable({ modelValue: [null, null] })
    const empty = await renderInTable({ modelValue: null })
    expect(inactive()).toEqual([])
    expect(empty()).toEqual([])

    const onUpdate = vi.fn()
    const active = await renderInTable({ modelValue: [from, to], 'onUpdate:modelValue': onUpdate })

    active()[0]?.clear()

    expect(onUpdate).toHaveBeenCalledWith(null)
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
