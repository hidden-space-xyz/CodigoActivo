import { reactive } from 'vue'
import { useMutation } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import { activityMutations } from '@/entities/activity'
import { alsoInvalidates } from '@/shared/api'
import { useCrudFeedback } from '@/shared/lib/feedback'

import { eventReportKeys } from '../api/queries'
import type { AttendeeAssignment, EventAttendee } from './types'

/** A signup an admin is changing: whose it is and which one. */
interface AssignmentTarget {
  readonly attendee: EventAttendee
  readonly assignment: AttendeeAssignment
}

/** One of the change dialogs: the signup it changes and the role or status picked so far. */
export interface AssignmentChangeDialog {
  visible: boolean
  target: AssignmentTarget | null
  selectedId: string | null
}

function closedDialog(): AssignmentChangeDialog {
  return { visible: false, target: null, selectedId: null }
}

/**
 * Role and status changes of the signups listed in an event's attendees tab. Each dialog opens on
 * the signup's current value; applying a change reports it with a toast, closes the dialog and
 * refreshes the event's counts and attendees. Call it in `setup`.
 *
 * @param eventId - Getter so the refreshed counts follow route changes.
 */
export function useAssignmentChanges(eventId: () => string) {
  const { t } = useI18n()
  const feedback = useCrudFeedback()

  const reports = () => [eventReportKeys.stats(eventId()), eventReportKeys.attendees()] as const
  const roleChange = useMutation(() =>
    alsoInvalidates(activityMutations.changeRole(), ...reports()),
  )
  const statusChange = useMutation(() =>
    alsoInvalidates(activityMutations.changeStatus(), ...reports()),
  )

  const roleDialog = reactive<AssignmentChangeDialog>(closedDialog())
  const statusDialog = reactive<AssignmentChangeDialog>(closedDialog())

  function openRole(attendee: EventAttendee, assignment: AttendeeAssignment): void {
    Object.assign(roleDialog, {
      visible: true,
      target: { attendee, assignment },
      selectedId: assignment.roleTypeId,
    })
  }

  function openStatus(attendee: EventAttendee, assignment: AttendeeAssignment): void {
    Object.assign(statusDialog, {
      visible: true,
      target: { attendee, assignment },
      selectedId: assignment.statusId,
    })
  }

  function applyRole(): void {
    const { target, selectedId } = roleDialog
    if (!target || !selectedId) return
    roleChange.mutate(
      {
        activityId: target.assignment.activityId,
        userId: target.attendee.userId,
        roleId: selectedId,
      },
      feedback.outcome(t('pages.admin.eventDetail.attendees.toast.roleUpdated'), () => {
        roleDialog.visible = false
      }),
    )
  }

  function applyStatus(): void {
    const { target, selectedId } = statusDialog
    if (!target || !selectedId) return
    statusChange.mutate(
      {
        activityId: target.assignment.activityId,
        userId: target.attendee.userId,
        statusId: selectedId,
      },
      feedback.outcome(t('pages.admin.eventDetail.attendees.toast.statusUpdated'), () => {
        statusDialog.visible = false
      }),
    )
  }

  return {
    roleDialog,
    statusDialog,
    changingRole: roleChange.isPending,
    changingStatus: statusChange.isPending,
    openRole,
    openStatus,
    applyRole,
    applyStatus,
  }
}
