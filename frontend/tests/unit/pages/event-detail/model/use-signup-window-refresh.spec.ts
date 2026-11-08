import { shallowRef } from 'vue'
import { describe, expect, it, vi } from 'vitest'

import { activityKeys } from '@/entities/activity'
import { eventKeys, type EventDetail } from '@/entities/event'
import { useSignupWindowRefresh } from '@/pages/event-detail/model/use-signup-window-refresh'

import { mountComposable } from '../../../../support/render'
import { buildEventDetail } from '../../../../support/models'

const MINUTE = 60_000
const DAY = 24 * 60 * MINUTE
const REFRESH = [
  { queryKey: eventKeys.detail('event-1') },
  { queryKey: activityKeys.ofEvent('event-1') },
]

async function setup(initial: EventDetail | null) {
  vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout', 'Date'] })
  vi.setSystemTime(new Date('2026-09-01T07:59:00Z'))
  const event = shallowRef<EventDetail | null>(initial)
  const rendered = await mountComposable(() => {
    useSignupWindowRefresh(() => event.value)
  })
  const invalidate = vi.spyOn(rendered.queryClient, 'invalidateQueries')
  const refreshes = () => invalidate.mock.calls.map(([filters]) => filters)
  return { ...rendered, event, refreshes }
}

describe('useSignupWindowRefresh', () => {
  it('refetches the event and its activities two seconds after signup opens', async () => {
    const { refreshes } = await setup(
      buildEventDetail({ status: 'upcoming', signupStartsAt: '2026-09-01T08:00:00Z' }),
    )

    await vi.advanceTimersByTimeAsync(MINUTE + 1_999)
    expect(refreshes()).toEqual([])

    await vi.advanceTimersByTimeAsync(1)
    expect(refreshes()).toEqual(REFRESH)
  })

  it('waits for each later change after a refetch: signup opening, then closing', async () => {
    const { refreshes } = await setup(
      buildEventDetail({
        status: 'upcoming',
        earlySignupStartsAt: '2026-09-01T08:00:00Z',
        signupStartsAt: '2026-09-01T09:00:00Z',
        signupEndsAt: '2026-09-01T10:00:00Z',
      }),
    )

    await vi.advanceTimersByTimeAsync(MINUTE + 2_000)
    expect(refreshes()).toHaveLength(2)
    await vi.advanceTimersByTimeAsync(60 * MINUTE)
    expect(refreshes()).toHaveLength(4)
    await vi.advanceTimersByTimeAsync(60 * MINUTE)
    expect(refreshes()).toEqual([...REFRESH, ...REFRESH, ...REFRESH])

    await vi.advanceTimersByTimeAsync(30 * DAY)
    expect(refreshes()).toHaveLength(6)
  })

  it('reaches a change further away than one timer can wait in several waits', async () => {
    const { refreshes } = await setup(
      buildEventDetail({
        status: 'upcoming',
        signupStartsAt: '2026-10-11T07:59:00Z',
        signupEndsAt: '2026-10-20T00:00:00Z',
      }),
    )

    await vi.advanceTimersByTimeAsync(30 * DAY)
    expect(refreshes()).toEqual([])

    await vi.advanceTimersByTimeAsync(10 * DAY + 2_000)
    expect(refreshes()).toEqual(REFRESH)
  })

  it('schedules nothing while the event is missing or once its signup has closed', async () => {
    const { event, refreshes } = await setup(null)
    expect(vi.getTimerCount()).toBe(0)

    event.value = buildEventDetail({
      status: 'signupClosed',
      signupStartsAt: '2026-08-01T08:00:00Z',
      signupEndsAt: '2026-08-31T20:00:00Z',
    })
    await vi.advanceTimersByTimeAsync(30 * DAY)

    expect(vi.getTimerCount()).toBe(0)
    expect(refreshes()).toEqual([])
  })

  it('follows a reloaded event and stops when its scope ends', async () => {
    const { event, wrapper, refreshes } = await setup(
      buildEventDetail({ status: 'upcoming', signupStartsAt: '2026-09-01T08:00:00Z' }),
    )

    event.value = buildEventDetail({ status: 'upcoming', signupStartsAt: '2026-09-01T09:00:00Z' })
    await vi.advanceTimersByTimeAsync(MINUTE + 2_000)
    expect(refreshes()).toEqual([])

    wrapper.unmount()
    await vi.advanceTimersByTimeAsync(DAY)
    expect(vi.getTimerCount()).toBe(0)
    expect(refreshes()).toEqual([])
  })
})
