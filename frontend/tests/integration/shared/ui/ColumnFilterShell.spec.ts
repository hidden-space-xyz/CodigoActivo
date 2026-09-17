import { flushPromises, type VueWrapper } from '@vue/test-utils'
import { ElPopover } from 'element-plus'
import { defineComponent, h } from 'vue'
import { describe, expect, it, vi } from 'vitest'

import ColumnFilterShell from '@/shared/ui/ColumnFilterShell.vue'

import { renderWithProviders, t } from '../../../support/render'

const baseProps = {
  label: 'Name',
  active: false,
  toggleLabel: 'Filter by name',
  clearLabel: 'Clear name filter',
  showClear: false,
}

async function open(wrapper: VueWrapper): Promise<HTMLElement> {
  await wrapper.find('.column-filter__toggle').trigger('click')
  await vi.waitFor(() => expect(wrapper.findComponent(ElPopover).props('visible')).toBe(true))
  let panel: HTMLElement | null = null
  await vi.waitFor(() => {
    panel = document.body.querySelector<HTMLElement>('.column-filter__panel')
    expect(panel).not.toBeNull()
  })
  return panel as unknown as HTMLElement
}

describe('ColumnFilterShell', () => {
  it('renders the header label and an inactive toggle', async () => {
    const { wrapper } = await renderWithProviders(ColumnFilterShell, {
      props: baseProps,
      attach: true,
    })

    expect(wrapper.find('.column-filter > span').text()).toBe('Name')
    const toggle = wrapper.find('.column-filter__toggle')
    expect(toggle.attributes('aria-label')).toBe('Filter by name')
    expect(toggle.attributes('title')).toBe('Filter by name')
    expect(toggle.classes()).not.toContain('column-filter__toggle--active')
    expect(toggle.find('path[fill-rule="evenodd"]').exists()).toBe(false)
  })

  it('highlights the toggle with a filled filter icon when active', async () => {
    const { wrapper } = await renderWithProviders(ColumnFilterShell, {
      props: { ...baseProps, active: true },
      attach: true,
    })

    const toggle = wrapper.find('.column-filter__toggle')
    expect(toggle.classes()).toContain('column-filter__toggle--active')
    expect(toggle.find('path[fill-rule="evenodd"]').exists()).toBe(true)
  })

  it('opens a panel with the slotted control and a clear button that emits clear', async () => {
    const { wrapper } = await renderWithProviders(ColumnFilterShell, {
      props: { ...baseProps, showClear: true, wide: true },
      slots: {
        default: (scope: { panel: HTMLElement | string }) =>
          h('input', { class: 'slotted', 'data-panel': typeof scope.panel }),
      },
      attach: true,
    })

    const panel = await open(wrapper)

    expect(panel.classList.contains('column-filter__panel--wide')).toBe(true)
    await vi.waitFor(() =>
      expect(panel.querySelector('.slotted')?.getAttribute('data-panel')).toBe('object'),
    )
    const clear = panel.querySelector<HTMLButtonElement>('.column-filter__clear')
    expect(clear?.getAttribute('aria-label')).toBe('Clear name filter')
    expect(clear?.getAttribute('title')).toBe(t('table.clear'))

    clear?.click()
    expect(wrapper.emitted('clear')).toHaveLength(1)
  })

  it('hides the clear button when there is nothing to clear', async () => {
    const { wrapper } = await renderWithProviders(ColumnFilterShell, {
      props: baseProps,
      attach: true,
    })

    const panel = await open(wrapper)

    expect(panel.classList.contains('column-filter__panel--wide')).toBe(false)
    expect(panel.querySelector('.column-filter__clear')).toBeNull()
  })

  it('emits show after opening and closes through the exposed hide', async () => {
    const Host = defineComponent({
      components: { ColumnFilterShell },
      emits: ['show'],
      setup(_, { emit, expose }) {
        const shell = { value: undefined as { hide: () => void } | undefined }
        expose({ hide: () => shell.value?.hide() })
        return () =>
          h(ColumnFilterShell, {
            ...baseProps,
            ref: (instance: unknown) => {
              shell.value = instance as { hide: () => void } | undefined
            },
            onShow: () => emit('show'),
          })
      },
    })
    const { wrapper } = await renderWithProviders(Host, { attach: true })
    await open(wrapper)
    const popover = wrapper.findComponent(ElPopover)
    expect(popover.props('visible')).toBe(true)

    popover.vm.$emit('after-enter')
    expect(wrapper.emitted('show')).toHaveLength(1)

    ;(wrapper.vm as unknown as { hide: () => void }).hide()
    await flushPromises()

    expect(popover.props('visible')).toBe(false)
  })
})
