import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { useCountdown } from '@/shared/lib/countdown'

import { mountComposable } from '../../../../support/render'

describe('useCountdown', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval', 'Date'] })
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('counts the seconds left down to zero and restarts on demand', async () => {
    const { result } = await mountComposable(() => useCountdown(3))
    expect(result.remaining.value).toBe(0)

    result.start()
    expect(result.remaining.value).toBe(3)
    vi.advanceTimersByTime(1000)
    expect(result.remaining.value).toBe(2)
    vi.advanceTimersByTime(5000)
    expect(result.remaining.value).toBe(0)

    result.start()
    expect(result.remaining.value).toBe(3)
  })

  it('clears at once when stopped and stops ticking with its component', async () => {
    const { result, wrapper } = await mountComposable(() => useCountdown(60))

    result.start()
    result.stop()
    expect(result.remaining.value).toBe(0)

    result.start()
    wrapper.unmount()
    vi.advanceTimersByTime(10_000)
    expect(result.remaining.value).toBe(60)
  })
})
