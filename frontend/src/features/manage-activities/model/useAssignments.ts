import type { MaybeRefOrGetter } from 'vue'
import { toValue } from 'vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'

import {
  patchApiActivitiesActivityIdUserIdChangeRole,
  patchApiActivitiesActivityIdUserIdChangeStatus,
} from '@/shared/api/generated/endpoints/activities/activities'
import type {
  ChangeAssignmentRoleRequest,
  ChangeAssignmentStatusRequest,
} from '@/shared/api/generated/models'

export function useAssignments(eventId: MaybeRefOrGetter<string>) {
  const queryClient = useQueryClient()
  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ['reports', 'event-summary', toValue(eventId)] })
    void queryClient.invalidateQueries({
      queryKey: ['reports', 'event-attendees', toValue(eventId)],
    })
  }

  const changeStatus = useMutation({
    mutationFn: (vars: {
      activityId: string
      userId: string
      body: ChangeAssignmentStatusRequest
    }) =>
      patchApiActivitiesActivityIdUserIdChangeStatus(vars.activityId, vars.userId, vars.body).then(
        (r) => r.data,
      ),
    onSuccess: invalidate,
  })

  const changeRole = useMutation({
    mutationFn: (vars: { activityId: string; userId: string; body: ChangeAssignmentRoleRequest }) =>
      patchApiActivitiesActivityIdUserIdChangeRole(vars.activityId, vars.userId, vars.body).then(
        (r) => r.data,
      ),
    onSuccess: invalidate,
  })

  return { changeStatus, changeRole }
}
