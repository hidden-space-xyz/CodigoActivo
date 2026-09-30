import { describe, expect, it, vi } from 'vitest'

import { useMediaQuery } from '@/shared/lib/media-query'

import { setMediaQueryMatches } from '../../../../support/media'

describe('useMediaQuery', () => {
  it('tracks the media query and reacts to changes', () => {
    const matches = useMediaQuery('(min-width: 9999px)')
    expect(matches.value).toBe(false)

    setMediaQueryMatches((query) => query === '(min-width: 9999px)')
    expect(matches.value).toBe(true)

    setMediaQueryMatches(() => false)
    expect(matches.value).toBe(false)
  })

  it('returns the same ref for the same query', () => {
    const matchMedia = vi.spyOn(window, 'matchMedia')

    const first = useMediaQuery('(orientation: portrait)')
    const second = useMediaQuery('(orientation: portrait)')

    expect(second).toBe(first)
    expect(matchMedia).toHaveBeenCalledTimes(1)
  })
})
