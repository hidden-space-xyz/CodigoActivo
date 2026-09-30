import { describe, expect, it, vi } from 'vitest'

import { eventReportKeys } from '@/pages/admin/event-detail/api/queries'
import { toEventAttendee } from '@/pages/admin/event-detail/api/mapper'
import { useAssignmentChanges } from '@/pages/admin/event-detail/model/use-assignment-changes'

import { apiError, http, HttpResponse, server } from '../../../../../support/server'
import { buildAttendee } from '../../../../../support/builders'
import { expectNotification } from '../../../../../support/dom'
import { mountComposable, t } from '../../../../../support/render'

const attendee = toEventAttendee(buildAttendee())
const [assignment] = attendee.assignments

function signup() {
  if (!assignment) throw new Error('Assignment not mapped')
  return assignment
}

describe('useAssignmentChanges', () => {
  it('opens each dialog on the current role or status of the signup', async () => {
    const { result } = await mountComposable(() => useAssignmentChanges(() => 'event-1'))

    result.openRole(attendee, signup())
    result.openStatus(attendee, signup())

    expect(result.roleDialog).toEqual({
      visible: true,
      target: { attendee, assignment: signup() },
      selectedId: 'role-1',
    })
    expect(result.statusDialog.selectedId).toBe('status-1')
  })

  it('changes the role and the status, refreshing the counts and attendees', async () => {
    const bodies: unknown[] = []
    server.use(
      http.patch('/api/activities/:activityId/:userId/change-role', async ({ request }) => {
        bodies.push(await request.json())
        return HttpResponse.json({ id: 'act-1' })
      }),
      http.patch('/api/activities/:activityId/:userId/change-status', async ({ request }) => {
        bodies.push(await request.json())
        return HttpResponse.json({ id: 'status-2', name: 'Confirmed' })
      }),
    )
    const { result, queryClient } = await mountComposable(() =>
      useAssignmentChanges(() => 'event-1'),
    )
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries')

    result.openRole(attendee, signup())
    result.roleDialog.selectedId = 'role-2'
    result.applyRole()
    await expectNotification(t('pages.admin.eventDetail.attendees.toast.roleUpdated'))
    expect(result.roleDialog.visible).toBe(false)

    result.openStatus(attendee, signup())
    result.statusDialog.selectedId = 'status-2'
    result.applyStatus()
    await expectNotification(t('pages.admin.eventDetail.attendees.toast.statusUpdated'))
    expect(result.statusDialog.visible).toBe(false)

    expect(bodies).toEqual([{ activityRoleTypeId: 'role-2' }, { assignmentStatusId: 'status-2' }])
    expect(invalidate).toHaveBeenCalledWith({ queryKey: eventReportKeys.stats('event-1') })
    expect(invalidate).toHaveBeenCalledWith({ queryKey: eventReportKeys.attendees() })
  })

  it('keeps the dialog open when the change fails and ignores an empty choice', async () => {
    let calls = 0
    server.use(
      http.patch('/api/activities/:activityId/:userId/change-status', () => {
        calls += 1
        return apiError(500)
      }),
    )
    const { result } = await mountComposable(() => useAssignmentChanges(() => 'event-1'))

    result.openStatus(attendee, signup())
    result.statusDialog.selectedId = null
    result.applyStatus()
    expect(calls).toBe(0)

    result.statusDialog.selectedId = 'status-2'
    result.applyStatus()
    await expectNotification(t('errors.generic'))
    expect(result.statusDialog.visible).toBe(true)
  })
})
