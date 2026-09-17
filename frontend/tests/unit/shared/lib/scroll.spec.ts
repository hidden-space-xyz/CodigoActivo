import { describe, expect, it, vi } from 'vitest'

import { scrollToTop } from '@/shared/lib'

describe('scrollToTop', () => {
  it('scrolls smoothly to the top', () => {
    const scrollTo = vi.spyOn(window, 'scrollTo').mockImplementation(() => undefined)

    scrollToTop()

    expect(scrollTo).toHaveBeenCalledWith({ top: 0, behavior: 'smooth' })
  })

  it('jumps to the top when scroll options are not supported', () => {
    const scrollTo = vi.spyOn(window, 'scrollTo').mockImplementation((...args: unknown[]) => {
      if (typeof args[0] === 'object') throw new TypeError('Options not supported')
    })

    scrollToTop()

    expect(scrollTo).toHaveBeenLastCalledWith(0, 0)
  })
})
