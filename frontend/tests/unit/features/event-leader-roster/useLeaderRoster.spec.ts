import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { useSession } from '@/entities/session'
import { useLeaderRoster } from '@/features/event-leader-roster'

import { renderComposable } from '../../../support/fixtures/entities/composable'
import {
  buildLeaderRosterActivity,
  serveLeaderRoster,
} from '../../../support/fixtures/public-dashboard/leader-roster'
import { buildAuthUser } from '../../../support/fixtures/user'

describe('useLeaderRoster', () => {
  it('does not ask a guest for the roster', async () => {
    const requests = serveLeaderRoster([buildLeaderRosterActivity()])

    const { result } = await renderComposable(() => useLeaderRoster('event-1'))
    await flushPromises()

    expect(requests).toEqual([])
    expect(result.activities.value).toEqual([])
    expect(result.hasActivities.value).toBe(false)
  })

  it('exposes the activities the signed-in user leads', async () => {
    serveLeaderRoster([buildLeaderRosterActivity()])
    useSession().setUser(buildAuthUser())

    const { result } = await renderComposable(() => useLeaderRoster(() => 'event-1'))

    await vi.waitFor(() => expect(result.hasActivities.value).toBe(true))
    expect(result.activities.value.map((activity) => activity.title)).toEqual([
      'Taller de robótica',
    ])
    expect(result.isError.value).toBe(false)
  })

  it('reports no activities for a user who leads none', async () => {
    const requests = serveLeaderRoster([])
    useSession().setUser(buildAuthUser())

    const { result } = await renderComposable(() => useLeaderRoster('event-1'))

    await vi.waitFor(() => expect(requests).toEqual(['event-1']))
    await flushPromises()
    expect(result.hasActivities.value).toBe(false)
    expect(result.isLoading.value).toBe(false)
  })

  it('drops the attendee data as soon as the session ends', async () => {
    serveLeaderRoster([buildLeaderRosterActivity()])
    const session = useSession()
    session.setUser(buildAuthUser())

    const { result } = await renderComposable(() => useLeaderRoster('event-1'))
    await vi.waitFor(() => expect(result.hasActivities.value).toBe(true))

    session.clear()
    await flushPromises()

    expect(result.activities.value).toEqual([])
    expect(result.hasActivities.value).toBe(false)
  })
})
