import { defineComponent, h, nextTick, reactive, watch, type VNode } from 'vue'
import { describe, expect, it, vi } from 'vitest'

import { ColumnFilterSelect, ColumnSearch, provideTableFilters } from '@/shared/ui/column-filter'

import { renderWithProviders } from '../../../../support/render'

interface Probe {
  label: string
  value: string | null
  shown: boolean
  onUpdate: (value: unknown) => void
}

async function renderRegistry(render: () => VNode | VNode[]) {
  const box: { active?: ReturnType<typeof provideTableFilters>['active'] } = {}
  const Host = defineComponent({
    setup() {
      box.active = provideTableFilters().active
      return () => h('div', render())
    },
  })
  await renderWithProviders(Host, { attach: true })
  if (!box.active) throw new Error('Registry was not provided')
  const active = box.active
  return {
    active,
    entries: () => active.value,
    described: () => active.value.map(({ label, value }) => ({ label, value })),
  }
}

async function renderSearches(probes: Pick<Probe, 'label' | 'value'>[]) {
  const state = reactive(
    probes.map((probe): Probe => ({ ...probe, shown: true, onUpdate: vi.fn() })),
  )
  const rendered = await renderRegistry(() =>
    state
      .filter((probe) => probe.shown)
      .map((probe) =>
        h(ColumnSearch, {
          key: probe.label,
          label: probe.label,
          modelValue: probe.value,
          'onUpdate:modelValue': probe.onUpdate,
        }),
      ),
  )
  return { ...rendered, state }
}

describe('filter registry', () => {
  it('lists the active filters in mount order and hides inactive ones', async () => {
    const { state, described } = await renderSearches([
      { label: 'Name', value: 'ada' },
      { label: 'Email', value: null },
      { label: 'Phone', value: '600' },
    ])

    expect(described()).toEqual([
      { label: 'Name', value: 'ada' },
      { label: 'Phone', value: '600' },
    ])

    const [, email] = state
    if (email) email.value = 'ada@'
    await nextTick()

    expect(described()).toEqual([
      { label: 'Name', value: 'ada' },
      { label: 'Email', value: 'ada@' },
      { label: 'Phone', value: '600' },
    ])
  })

  it('gives each active filter its own identity and clear action', async () => {
    const { state, entries } = await renderSearches([
      { label: 'Name', value: 'ada' },
      { label: 'Phone', value: '600' },
    ])

    const [name, phone] = entries()
    expect(name?.id).not.toBe(phone?.id)

    phone?.clear()

    expect(state[1]?.onUpdate).toHaveBeenCalledWith(null)
    expect(state[0]?.onUpdate).not.toHaveBeenCalled()
  })

  it('forgets a filter once it unmounts', async () => {
    const { state, described } = await renderSearches([
      { label: 'Name', value: 'ada' },
      { label: 'Phone', value: '600' },
    ])

    const [name] = state
    if (name) name.shown = false
    await nextTick()

    expect(described()).toEqual([{ label: 'Phone', value: '600' }])
  })

  it('only reports a change when the described text changes', async () => {
    const state = reactive({ options: [{ label: 'Active', value: 'active' }] })
    const { active, described } = await renderRegistry(() =>
      h(ColumnFilterSelect, { label: 'Status', modelValue: 'active', options: state.options }),
    )
    const changes = vi.fn()
    watch(active, changes)

    state.options = [{ label: 'Active', value: 'active' }]
    await nextTick()

    expect(changes).not.toHaveBeenCalled()

    state.options = [{ label: 'Activo', value: 'active' }]
    await nextTick()

    expect(changes).toHaveBeenCalledTimes(1)
    expect(described()).toEqual([{ label: 'Status', value: 'Activo' }])
  })

  it('leaves filters outside a provider working on their own', async () => {
    const { wrapper } = await renderWithProviders(ColumnSearch, {
      props: { label: 'Name', modelValue: 'ada' },
    })

    expect(wrapper.find('.column-filter__toggle--active').exists()).toBe(true)
    wrapper.unmount()
  })
})
