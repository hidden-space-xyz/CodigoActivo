import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { SearchInput } from '@/shared/ui'

import { renderWithProviders } from '../../../support/render'

async function renderSearch(props: Record<string, unknown> = {}) {
  const rendered = await renderWithProviders(SearchInput, {
    props: {
      modelValue: '',
      label: 'Search events',
      'onUpdate:modelValue': (value: string) => rendered.wrapper.setProps({ modelValue: value }),
      ...props,
    },
    attach: true,
  })
  const input = rendered.wrapper.find('input')
  return { ...rendered, input }
}

describe('SearchInput', () => {
  it('uses the label as placeholder and accessible name', async () => {
    const { wrapper, input } = await renderSearch()

    expect(input.attributes('placeholder')).toBe('Search events')
    expect(input.attributes('aria-label')).toBe('Search events')
    expect(input.attributes('enterkeyhint')).toBe('search')
    expect(wrapper.find('.el-input__prefix .app-icon').exists()).toBe(true)
  })

  it('emits the trimmed term after the debounce', async () => {
    vi.useFakeTimers()
    const { wrapper, input } = await renderSearch({ debounce: 500 })

    await input.setValue(' hack')
    await input.setValue(' hackathon ')
    vi.advanceTimersByTime(499)
    expect(wrapper.emitted('update:modelValue')).toBeUndefined()

    vi.advanceTimersByTime(1)
    await flushPromises()

    expect(wrapper.emitted('update:modelValue')).toEqual([['hackathon']])
    expect((input.element as HTMLInputElement).value).toBe(' hackathon ')
  })

  it('emits immediately on Enter', async () => {
    vi.useFakeTimers()
    const { wrapper, input } = await renderSearch()

    await input.setValue('robots')
    await input.trigger('keydown', { key: 'Enter' })
    vi.runAllTimers()

    expect(wrapper.emitted('update:modelValue')).toEqual([['robots']])
  })

  it('skips emitting when the term did not change', async () => {
    vi.useFakeTimers()
    const { wrapper, input } = await renderSearch({ modelValue: 'same' })

    await input.setValue('  same  ')
    vi.runAllTimers()

    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
  })

  it('emits an empty term when cleared', async () => {
    const { wrapper } = await renderSearch({ modelValue: 'old' })

    await wrapper.find('.el-input__clear').trigger('mousedown')
    await wrapper.find('.el-input__clear').trigger('click')
    await flushPromises()

    expect(wrapper.emitted('update:modelValue')).toEqual([['']])
  })

  it('follows external model changes', async () => {
    const { wrapper, input } = await renderSearch({ modelValue: 'first' })

    await wrapper.setProps({ modelValue: 'second' })

    expect((input.element as HTMLInputElement).value).toBe('second')
  })

  it('cancels a pending debounce when unmounted', async () => {
    vi.useFakeTimers()
    const onUpdate = vi.fn()
    const { wrapper, input } = await renderSearch({ 'onUpdate:modelValue': onUpdate })

    await input.setValue('pending')
    wrapper.unmount()
    vi.runAllTimers()

    expect(onUpdate).not.toHaveBeenCalled()
  })
})
