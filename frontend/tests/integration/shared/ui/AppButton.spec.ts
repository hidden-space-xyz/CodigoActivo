import { ElButton, ElTooltip } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import { AppButton } from '@/shared/ui'

import { setMediaQueryMatches } from '../../../support/media'
import { renderWithProviders } from '../../../support/render'

describe('AppButton', () => {
  it('renders the label attribute as text with the label as tooltip', async () => {
    const onClick = vi.fn()
    const { wrapper } = await renderWithProviders(AppButton, {
      props: { label: 'Save', type: 'primary', icon: ' plus ', onClick },
    })

    const button = wrapper.find('button')
    expect(button.text()).toBe('Save')
    expect(button.classes()).toContain('el-button--primary')
    expect(button.attributes('label')).toBeUndefined()
    expect(button.find('.app-icon').exists()).toBe(true)
    expect(wrapper.findComponent(ElTooltip).props('content')).toBe('Save')
    expect(wrapper.findComponent(ElTooltip).props('disabled')).toBe(false)

    await button.trigger('click')
    expect(onClick).toHaveBeenCalledTimes(1)
  })

  it('prefers the default slot and an explicit tooltip', async () => {
    const { wrapper } = await renderWithProviders(AppButton, {
      props: { tooltip: 'Explains the action', 'aria-label': 'Aria name' },
      slots: { default: 'Slot text' },
    })

    expect(wrapper.find('button').text()).toBe('Slot text')
    expect(wrapper.findComponent(ElTooltip).props('content')).toBe('Explains the action')
    expect(wrapper.findComponent(ElButton).props('icon')).toBe('')
  })

  it('names icon-only buttons after the tooltip or aria-label', async () => {
    const withTooltip = await renderWithProviders(AppButton, {
      props: { icon: 'trash', tooltip: 'Delete row' },
    })
    const withAria = await renderWithProviders(AppButton, {
      props: { icon: 'pencil', 'aria-label': 'Edit row' },
    })

    expect(withTooltip.wrapper.find('button').attributes('aria-label')).toBe('Delete row')
    expect(withAria.wrapper.find('button').attributes('aria-label')).toBe('Edit row')
    expect(withAria.wrapper.findComponent(ElTooltip).props('content')).toBe('Edit row')
  })

  it('disables the tooltip without text or on coarse pointers', async () => {
    const silent = await renderWithProviders(AppButton, { props: { icon: 'times' } })
    expect(silent.wrapper.find('button').attributes('aria-label')).toBeUndefined()
    expect(silent.wrapper.findComponent(ElTooltip).props('disabled')).toBe(true)

    setMediaQueryMatches((query) => query === '(pointer: coarse)')
    const touch = await renderWithProviders(AppButton, { props: { label: 'Touch me' } })

    expect(touch.wrapper.findComponent(ElTooltip).props('disabled')).toBe(true)
  })
})
