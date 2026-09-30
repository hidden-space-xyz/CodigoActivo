import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { endSession } from '@/entities/session'
import { useLeaderRoster } from '@/pages/event-detail/model/use-leader-roster'
import { serveLeaderRoster } from '../../../../support/api/leader-roster'
import { buildLeaderRosterActivity } from '../../../../support/builders'
import { mountComposable } from '../../../../support/render'

describe('useLeaderRoster', () => {
  it('does not ask a guest for the roster', async () => {
    const requests = serveLeaderRoster([buildLeaderRosterActivity()])

    const { result } = await mountComposable(() => useLeaderRoster('event-1'))
    await flushPromises()

    expect(requests).toEqual([])
    expect(result.activities.value).toEqual([])
    expect(result.hasActivities.value).toBe(false)
  })

  it('exposes the activities the signed-in user leads', async () => {
    serveLeaderRoster([buildLeaderRosterActivity()])
    const { result } = await mountComposable(() => useLeaderRoster(() => 'event-1'), { user: {} })

    await vi.waitFor(() => expect(result.hasActivities.value).toBe(true))
    expect(result.activities.value.map((activity) => activity.title)).toEqual([
      'Taller de robótica',
    ])
    expect(result.isError.value).toBe(false)
  })

  it('reports no activities for a user who leads none', async () => {
    const requests = serveLeaderRoster([])
    const { result } = await mountComposable(() => useLeaderRoster('event-1'), { user: {} })

    await vi.waitFor(() => expect(requests).toEqual(['event-1']))
    await flushPromises()
    expect(result.hasActivities.value).toBe(false)
    expect(result.isLoading.value).toBe(false)
  })

  it('drops the attendee data as soon as the session ends', async () => {
    serveLeaderRoster([buildLeaderRosterActivity()])
    const { result, queryClient } = await mountComposable(() => useLeaderRoster('event-1'), {
      user: {},
    })
    await vi.waitFor(() => expect(result.hasActivities.value).toBe(true))

    endSession(queryClient)
    await flushPromises()

    expect(result.activities.value).toEqual([])
    expect(result.hasActivities.value).toBe(false)
  })
})
