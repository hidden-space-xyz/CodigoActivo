import type { MaybeRefOrGetter } from 'vue'
import { toValue } from 'vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'

import type {
  ChangeAssignmentRoleRequest,
  ChangeAssignmentStatusRequest,
} from '@/shared/api/generated/models'
import { changeAssignmentRoleRequest, changeAssignmentStatusRequest } from '@/entities/activity'
import { eventReportQueryKeys } from '@/entities/event'

export function useAssignments(eventId: MaybeRefOrGetter<string>) {
  const queryClient = useQueryClient()
  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: eventReportQueryKeys.summary(toValue(eventId)) })
    void queryClient.invalidateQueries({ queryKey: eventReportQueryKeys.attendees() })
  }

  const changeStatus = useMutation({
    mutationFn: (vars: {
      activityId: string
      userId: string
      body: ChangeAssignmentStatusRequest
    }) => changeAssignmentStatusRequest(vars.activityId, vars.userId, vars.body),
    onSuccess: invalidate,
  })

  const changeRole = useMutation({
    mutationFn: (vars: { activityId: string; userId: string; body: ChangeAssignmentRoleRequest }) =>
      changeAssignmentRoleRequest(vars.activityId, vars.userId, vars.body),
    onSuccess: invalidate,
  })

  return { changeStatus, changeRole }
}
