import { flushPromises } from '@vue/test-utils'
import { ElDatePicker } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import RangeFilter from '@/pages/admin/dashboard/ui/RangeFilter.vue'
import { formatDate } from '@/shared/lib'

import { setMediaQueryMatches } from '../../../../support/media'
import { renderWithProviders, t } from '../../../../support/render'

async function renderFilter(props: { preset: string; customRange: (Date | null)[] | null }) {
  return renderWithProviders(RangeFilter, {
    props,
    attach: true,
    stubs: { transition: false },
  })
}

describe('RangeFilter', () => {
  it('renders the presets and marks the active one', async () => {
    const { wrapper } = await renderFilter({ preset: '90d', customRange: null })

    const pills = wrapper.findAll('.range-filter__pill')
    expect(pills.map((pill) => pill.text())).toEqual([
      t('pages.admin.dashboard.range.preset30d'),
      t('pages.admin.dashboard.range.preset90d'),
      t('pages.admin.dashboard.range.preset12m'),
      t('pages.admin.dashboard.range.custom'),
    ])
    expect(pills.map((pill) => pill.attributes('aria-pressed'))).toEqual([
      'false',
      'true',
      'false',
      'false',
    ])
  })

  it('emits the clicked preset', async () => {
    const { wrapper } = await renderFilter({ preset: '12m', customRange: null })

    await wrapper.findAll('.range-filter__pill')[0]?.trigger('click')

    expect(wrapper.emitted('preset')).toEqual([['30d']])
  })

  it('labels the calendar pill with the committed custom dates', async () => {
    const start = new Date(2026, 0, 5)
    const end = new Date(2026, 1, 10)
    const { wrapper } = await renderFilter({ preset: 'custom', customRange: [start, end] })

    const custom = wrapper.find('.range-filter__pill--custom')
    expect(custom.attributes('aria-pressed')).toBe('true')
    expect(custom.text()).toBe(
      `${formatDate(start.toISOString())} – ${formatDate(end.toISOString())}`,
    )
  })

  it('repeats the start date when the custom range has no end', async () => {
    const start = new Date(2026, 0, 5)
    const { wrapper } = await renderFilter({ preset: 'custom', customRange: [start, null] })

    const label = formatDate(start.toISOString())
    expect(wrapper.find('.range-filter__pill--custom').text()).toBe(`${label} – ${label}`)
  })

  it('keeps the generic label when dates exist but another preset is active', async () => {
    const { wrapper } = await renderFilter({
      preset: '30d',
      customRange: [new Date(2026, 0, 5), new Date(2026, 1, 10)],
    })

    expect(wrapper.find('.range-filter__pill--custom').text()).toBe(
      t('pages.admin.dashboard.range.custom'),
    )
  })

  it('opens the picker seeded with the committed dates and emits a complete range', async () => {
    const start = new Date(2026, 0, 5)
    const end = new Date(2026, 1, 10)
    const { wrapper } = await renderFilter({ preset: 'custom', customRange: [start, end] })

    const picker = wrapper.findComponent(ElDatePicker)
    picker.vm.$emit('update:modelValue', [new Date(2026, 2, 1)])
    await flushPromises()
    expect(picker.props('modelValue')).toEqual([new Date(2026, 2, 1)])

    // Opening the popover discards the uncommitted draft and shows the committed dates again.
    await wrapper.find('.range-filter__pill--custom').trigger('click')
    await vi.waitFor(() => expect(picker.props('modelValue')).toEqual([start, end]))
    expect(picker.props('unlinkPanels')).toBe(true)

    picker.vm.$emit('update:modelValue', [new Date(2026, 2, 1)])
    await flushPromises()
    expect(wrapper.emitted('range')).toBeUndefined()
    expect(picker.props('modelValue')).toEqual([new Date(2026, 2, 1)])

    picker.vm.$emit('update:modelValue', null)
    await flushPromises()
    expect(picker.props('modelValue')).toEqual([])

    // Clicks and key presses inside the panel must not reach the popover trigger and close it.
    const panel = document.body.querySelector<HTMLElement>('.range-filter__panel')
    const outerEvents = vi.fn()
    document.body.addEventListener('click', outerEvents)
    document.body.addEventListener('keydown', outerEvents)
    panel?.dispatchEvent(new MouseEvent('click', { bubbles: true }))
    panel?.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }))
    document.body.removeEventListener('click', outerEvents)
    document.body.removeEventListener('keydown', outerEvents)
    expect(panel).not.toBeNull()
    expect(outerEvents).not.toHaveBeenCalled()

    const picked = [new Date(2026, 2, 1), new Date(2026, 2, 15)]
    picker.vm.$emit('update:modelValue', picked)
    await flushPromises()
    expect(wrapper.emitted('range')).toEqual([[picked]])
  })

  it('uses a single editable panel on narrow screens', async () => {
    setMediaQueryMatches((query) => query === '(max-width: 640px)')
    const { wrapper } = await renderFilter({ preset: '12m', customRange: null })

    const picker = wrapper.findComponent(ElDatePicker)
    expect(picker.exists()).toBe(true)
    expect(picker.props('unlinkPanels')).toBe(false)
    expect(picker.props('editable')).toBe(true)
  })
})
