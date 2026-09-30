import { describe, expect, it, vi } from 'vitest'

import { PrintToolbar } from '@/shared/ui/print-toolbar'

import { renderWithProviders } from '../../../../support/render'

function hasPageRule(): boolean {
  return [...document.head.querySelectorAll('style')].some((style) =>
    style.textContent?.includes('@page { size: A4 portrait; margin: 0; }'),
  )
}

describe('PrintToolbar', () => {
  it('links back, prints on demand and sets A4 pages while mounted', async () => {
    const print = vi.spyOn(window, 'print').mockImplementation(() => undefined)
    const { wrapper } = await renderWithProviders(PrintToolbar, {
      props: { back: { name: 'admin-events' }, backLabel: 'Back', printLabel: 'Print' },
    })

    const back = wrapper.find('.print-toolbar__back')
    expect(back.text()).toContain('Back')
    expect(back.attributes('href')).toBe('/admin/events')
    expect(hasPageRule()).toBe(true)

    await wrapper.find('.print-toolbar__print').trigger('click')
    expect(print).toHaveBeenCalledTimes(1)
    expect(wrapper.find('.print-toolbar__print').text()).toBe('Print')

    wrapper.unmount()
    expect(hasPageRule()).toBe(false)
  })
})
