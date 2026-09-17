import { flushPromises, type VueWrapper } from '@vue/test-utils'
import { ElInput, ElPopover } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import { ColumnSearch } from '@/shared/ui'

import { renderWithProviders, t } from '../../../support/render'

type Value = string | number | null | undefined

async function renderSearch(props: Record<string, unknown> = {}) {
  const rendered = await renderWithProviders(ColumnSearch, {
    props: {
      modelValue: null,
      label: 'Email',
      'onUpdate:modelValue': (value: Value) => rendered.wrapper.setProps({ modelValue: value }),
      ...props,
    },
    attach: true,
  })
  return rendered
}

async function open(wrapper: VueWrapper): Promise<HTMLInputElement> {
  await wrapper.find('.column-filter__toggle').trigger('click')
  await vi.waitFor(() => expect(wrapper.findComponent(ElPopover).props('visible')).toBe(true))
  const input = document.body.querySelector<HTMLInputElement>('.column-filter__panel input')
  if (!input) throw new Error('Search input not rendered')
  return input
}

async function type(input: HTMLInputElement, value: string): Promise<void> {
  input.value = value
  input.dispatchEvent(new Event('input'))
  await flushPromises()
}

function key(input: HTMLInputElement, name: string): void {
  input.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true }))
}

function visible(wrapper: VueWrapper): unknown {
  return wrapper.findComponent(ElPopover).props('visible')
}

describe('ColumnSearch', () => {
  it('labels the toggle and input after the column', async () => {
    const { wrapper } = await renderSearch()

    const label = t('table.searchBy', { label: 'Email' })
    expect(wrapper.find('.column-filter__toggle').attributes('aria-label')).toBe(label)
    expect(wrapper.findComponent(ElInput).props('placeholder')).toBe(label)
    expect(wrapper.findComponent(ElInput).props('type')).toBe('text')
  })

  it('uses a custom placeholder', async () => {
    const { wrapper } = await renderSearch({ placeholder: 'user@example.test' })

    expect(wrapper.findComponent(ElInput).props('placeholder')).toBe('user@example.test')
  })

  it('emits the trimmed term after the debounce', async () => {
    const { wrapper } = await renderSearch({ debounce: 200 })
    const input = await open(wrapper)
    vi.useFakeTimers()

    await type(input, '  ada')
    await type(input, '  ada@example.test ')
    vi.advanceTimersByTime(199)
    expect(wrapper.emitted('update:modelValue')).toBeUndefined()

    vi.advanceTimersByTime(1)
    await flushPromises()

    expect(wrapper.emitted('update:modelValue')).toEqual([['ada@example.test']])
    expect(wrapper.emitted('apply')).toHaveLength(1)
    expect(input.value).toBe('  ada@example.test ')
    expect(wrapper.find('.column-filter__toggle').classes()).toContain(
      'column-filter__toggle--active',
    )
  })

  it('converts numeric searches to numbers', async () => {
    const { wrapper } = await renderSearch({ inputType: 'number', modelValue: 5 })
    const input = await open(wrapper)
    vi.useFakeTimers()

    await type(input, '42')
    vi.runAllTimers()

    expect(wrapper.findComponent(ElInput).props('type')).toBe('number')
    expect(wrapper.emitted('update:modelValue')).toEqual([[42]])
  })

  it('emits null for a blank search', async () => {
    const { wrapper } = await renderSearch({ modelValue: 'ada' })
    const input = await open(wrapper)
    vi.useFakeTimers()

    await type(input, '   ')
    vi.runAllTimers()
    await flushPromises()

    expect(wrapper.emitted('update:modelValue')).toEqual([[null]])
    expect(wrapper.find('.column-filter__toggle').classes()).not.toContain(
      'column-filter__toggle--active',
    )
  })

  it('applies immediately on Enter and closes the popover', async () => {
    const { wrapper } = await renderSearch()
    const input = await open(wrapper)
    vi.useFakeTimers()

    await type(input, 'grace')
    key(input, 'Enter')
    await flushPromises()
    vi.runAllTimers()

    expect(wrapper.emitted('update:modelValue')).toEqual([['grace']])
    await vi.waitFor(() => expect(visible(wrapper)).toBe(false))
  })

  it('discards the draft on Escape without emitting', async () => {
    const { wrapper } = await renderSearch({ modelValue: 'kept' })
    const input = await open(wrapper)
    vi.useFakeTimers()

    await type(input, 'draft')
    key(input, 'Escape')
    await flushPromises()
    vi.runAllTimers()

    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
    expect(wrapper.findComponent(ElInput).props('modelValue')).toBe('kept')
    await vi.waitFor(() => expect(visible(wrapper)).toBe(false))
  })

  it('closes on Escape before anything was typed', async () => {
    const { wrapper } = await renderSearch({ modelValue: 7 })
    const input = await open(wrapper)

    key(input, 'Escape')
    await flushPromises()

    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
    expect(wrapper.findComponent(ElInput).props('modelValue')).toBe('7')
    await vi.waitFor(() => expect(visible(wrapper)).toBe(false))
  })

  it('restores an empty draft on Escape without a model value', async () => {
    const { wrapper } = await renderSearch()
    const input = await open(wrapper)

    await type(input, 'draft')
    key(input, 'Escape')
    await flushPromises()

    expect(wrapper.findComponent(ElInput).props('modelValue')).toBe('')
  })

  it('clears the search from the clear button', async () => {
    const { wrapper } = await renderSearch({ modelValue: 'ada' })
    await open(wrapper)

    const clear = document.body.querySelector<HTMLButtonElement>('.column-filter__clear')
    expect(clear?.getAttribute('aria-label')).toBe(t('table.clearSearch'))
    clear?.click()
    await flushPromises()

    expect(wrapper.emitted('update:modelValue')).toEqual([[null]])
    expect(wrapper.emitted('apply')).toHaveLength(1)
    expect(document.body.querySelector('.column-filter__clear')).toBeNull()
  })

  it('focuses the input once the popover has opened', async () => {
    const { wrapper } = await renderSearch()
    const input = await open(wrapper)

    wrapper.findComponent(ElPopover).vm.$emit('after-enter')
    await flushPromises()

    expect(document.activeElement).toBe(input)
  })

  it('syncs the draft with external model changes', async () => {
    const { wrapper } = await renderSearch()

    await wrapper.setProps({ modelValue: 12 })
    expect(wrapper.findComponent(ElInput).props('modelValue')).toBe('12')

    await wrapper.setProps({ modelValue: undefined })
    expect(wrapper.findComponent(ElInput).props('modelValue')).toBe('')
  })

  it('keeps an untrimmed draft when the model matches its trimmed value', async () => {
    const { wrapper } = await renderSearch({ debounce: 0 })
    const input = await open(wrapper)
    vi.useFakeTimers()

    await type(input, 'ada ')
    vi.runAllTimers()
    await flushPromises()

    expect(wrapper.props('modelValue')).toBe('ada')
    expect(wrapper.findComponent(ElInput).props('modelValue')).toBe('ada ')
  })

  it('cancels a pending debounce when unmounted', async () => {
    const { wrapper } = await renderSearch()
    const input = await open(wrapper)
    vi.useFakeTimers()
    const onUpdate = vi.fn()
    await wrapper.setProps({ 'onUpdate:modelValue': onUpdate })

    await type(input, 'pending')
    wrapper.unmount()
    vi.runAllTimers()

    expect(onUpdate).not.toHaveBeenCalled()
  })
})
