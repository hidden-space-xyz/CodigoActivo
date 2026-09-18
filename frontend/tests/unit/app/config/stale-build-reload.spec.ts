import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { registerStaleBuildReload } from '@/app/config'

interface FakeWindow {
  readonly target: Window
  readonly reload: ReturnType<typeof vi.fn>
  readonly dispatch: () => Event
}

function createWindow(storage: Pick<Storage, 'getItem' | 'setItem'>, online = true): FakeWindow {
  const events = new EventTarget()
  const reload = vi.fn()
  const target = {
    addEventListener: events.addEventListener.bind(events),
    sessionStorage: storage,
    navigator: { onLine: online },
    location: { reload },
  } as unknown as Window

  return {
    target,
    reload,
    dispatch: () => {
      const event = new Event('vite:preloadError', { cancelable: true })
      events.dispatchEvent(event)
      return event
    },
  }
}

function createMemoryStorage(): Pick<Storage, 'getItem' | 'setItem'> {
  const values = new Map<string, string>()
  return {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => {
      values.set(key, value)
    },
  }
}

describe('registerStaleBuildReload', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-01-01T10:00:00Z'))
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('reloads once and suppresses the error when a chunk fails to load', () => {
    const fake = createWindow(createMemoryStorage())
    registerStaleBuildReload(fake.target)

    const event = fake.dispatch()

    expect(fake.reload).toHaveBeenCalledTimes(1)
    expect(event.defaultPrevented).toBe(true)
  })

  it('lets a repeated failure surface instead of reloading in a loop', () => {
    const fake = createWindow(createMemoryStorage())
    registerStaleBuildReload(fake.target)
    fake.dispatch()
    vi.advanceTimersByTime(59_000)

    const event = fake.dispatch()

    expect(fake.reload).toHaveBeenCalledTimes(1)
    expect(event.defaultPrevented).toBe(false)
  })

  it('reloads again once the cooldown has elapsed', () => {
    const fake = createWindow(createMemoryStorage())
    registerStaleBuildReload(fake.target)
    fake.dispatch()
    vi.advanceTimersByTime(60_000)

    fake.dispatch()

    expect(fake.reload).toHaveBeenCalledTimes(2)
  })

  it('does not reload while the browser is offline', () => {
    const fake = createWindow(createMemoryStorage(), false)
    registerStaleBuildReload(fake.target)

    const event = fake.dispatch()

    expect(fake.reload).not.toHaveBeenCalled()
    expect(event.defaultPrevented).toBe(false)
  })

  it('does not reload when session storage is unavailable', () => {
    const fake = createWindow({
      getItem: () => {
        throw new Error('denied')
      },
      setItem: () => undefined,
    })
    registerStaleBuildReload(fake.target)

    const event = fake.dispatch()

    expect(fake.reload).not.toHaveBeenCalled()
    expect(event.defaultPrevented).toBe(false)
  })
})
