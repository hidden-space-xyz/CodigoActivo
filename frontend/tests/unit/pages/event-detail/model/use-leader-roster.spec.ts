import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { endSession } from '@/entities/session'
import { useLeaderRoster } from '@/pages/event-detail/model/use-leader-roster'
import { serveLeaderRoster } from '../../../../support/api/leader-roster'
import { buildLeaderRosterActivity } from '../../../../support/builders'
import { mountComposable } from '../../../../support/render'

describe('useLeaderRoster', () => {
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
