import { describe, expect, it, vi } from 'vitest'

import { useAssignments } from '@/features/manage-activities'

import { EVENT_ID, setupComposable } from '../../../support/fixtures/admin-events/builders'
import { http, HttpResponse, server } from '../../../support/server'

describe('useAssignments', () => {
  it('changes status and role and refreshes the event reports', async () => {
    const calls: { path: string; body: unknown }[] = []
    server.use(
      http.patch('/api/activities/:activityId/:userId/change-status', async ({ request }) => {
        calls.push({ path: new URL(request.url).pathname, body: await request.json() })
        return HttpResponse.json({})
      }),
      http.patch('/api/activities/:activityId/:userId/change-role', async ({ request }) => {
        calls.push({ path: new URL(request.url).pathname, body: await request.json() })
        return HttpResponse.json({})
      }),
    )

    const { result, queryClient } = await setupComposable(() => useAssignments(() => EVENT_ID))
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries')

    await result.changeStatus.mutateAsync({
      activityId: 'act-1',
      userId: 'user-1',
      body: { assignmentStatusId: 'status-2' },
    })
    await result.changeRole.mutateAsync({
      activityId: 'act-1',
      userId: 'user-1',
      body: { activityRoleTypeId: 'role-2' },
    })

    expect(calls).toEqual([
      {
        path: '/api/activities/act-1/user-1/change-status',
        body: { assignmentStatusId: 'status-2' },
      },
      { path: '/api/activities/act-1/user-1/change-role', body: { activityRoleTypeId: 'role-2' } },
    ])
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['reports', 'event-summary', EVENT_ID] })
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['reports', 'event-attendees'] })
    expect(invalidate).toHaveBeenCalledTimes(4)
  })
})
