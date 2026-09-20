import { describe, expect, it, vi } from 'vitest'

import { BaseButton } from '@/shared/ui'

import { renderWithProviders } from '../../../support/render'

describe('BaseButton', () => {
  it('renders a native primary button by default', async () => {
    const onClick = vi.fn()
    const { wrapper } = await renderWithProviders(BaseButton, {
      props: { onClick },
      slots: { default: 'Continue' },
    })

    const button = wrapper.find('button')
    expect(button.text()).toBe('Continue')
    expect(button.attributes('type')).toBe('button')
    expect(button.attributes('disabled')).toBeUndefined()
    expect(button.attributes('aria-busy')).toBeUndefined()
    expect(button.classes()).toEqual(['base-button', 'base-button--primary'])

    await button.trigger('click')
    expect(onClick).toHaveBeenCalledTimes(1)
  })

  it('renders a disabled submit button with block and variant classes', async () => {
    const { wrapper } = await renderWithProviders(BaseButton, {
      props: { type: 'submit', variant: 'ghost', block: true, disabled: true },
      slots: { default: 'Send' },
    })

    const button = wrapper.find('button')
    expect(button.attributes('type')).toBe('submit')
    expect(button.attributes('disabled')).toBeDefined()
    expect(button.classes()).toEqual(
      expect.arrayContaining(['base-button--ghost', 'base-button--block', 'base-button--disabled']),
    )
  })

  it('renders the shared back treatment with a vector arrow', async () => {
    const { wrapper } = await renderWithProviders(BaseButton, {
      props: { variant: 'back' },
      slots: { default: 'Volver' },
    })

    const button = wrapper.find('button')
    expect(button.classes()).toContain('base-button--back')
    expect(button.text()).toBe('Volver')
    expect(button.find('.base-button__back-icon').exists()).toBe(true)
  })

  it('shows a spinner, marks itself busy and disables the button while loading', async () => {
    const { wrapper } = await renderWithProviders(BaseButton, {
      props: { loading: true },
      slots: { default: 'Saving' },
    })

    const button = wrapper.find('button')
    expect(button.attributes('aria-busy')).toBe('true')
    expect(button.attributes('disabled')).toBeDefined()
    expect(button.classes()).toContain('base-button--loading')
    expect(button.find('.base-button__spinner.app-icon--spin').exists()).toBe(true)
  })

  it('renders a router link when a route is given', async () => {
    const { wrapper, router } = await renderWithProviders(BaseButton, {
      props: { to: { name: 'events' }, href: '/ignored', variant: 'light', disabled: true },
      slots: { default: 'Events' },
    })

    const link = wrapper.find('a')
    expect(link.attributes('href')).toBe('/events')
    expect(link.classes()).toContain('base-button--light')
    expect(link.classes()).not.toContain('base-button--disabled')
    expect(wrapper.find('button').exists()).toBe(false)

    await link.trigger('click')
    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('events'))
  })

  it('renders a plain anchor for an external URL', async () => {
    const { wrapper } = await renderWithProviders(BaseButton, {
      props: { href: 'https://example.test', variant: 'link', loading: true },
      slots: { default: 'Visit' },
    })

    const link = wrapper.find('a')
    expect(link.attributes('href')).toBe('https://example.test')
    expect(link.attributes('type')).toBeUndefined()
    expect(link.classes()).toEqual(
      expect.arrayContaining(['base-button--link', 'base-button--loading']),
    )
    expect(link.classes()).not.toContain('base-button--disabled')
  })
})
