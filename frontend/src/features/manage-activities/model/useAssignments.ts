import type { MaybeRefOrGetter } from 'vue'
import { toValue } from 'vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'

import {
  patchApiActivitiesActivityIdUserIdAssign,
  patchApiActivitiesActivityIdUserIdChangeRole,
  patchApiActivitiesActivityIdUserIdChangeStatus,
  patchApiActivitiesActivityIdUserIdUnassign,
} from '@/shared/api/generated/endpoints/activities/activities'
import type {
  AssignRequest,
  ChangeAssignmentRoleRequest,
  ChangeAssignmentStatusRequest,
} from '@/shared/api/generated/models'
import { verifyOverlapsRequest } from '@/entities/activity'

export function useAssignments(eventId: MaybeRefOrGetter<string>) {
  const queryClient = useQueryClient()
  const invalidate = (_data: unknown, vars: { activityId: string }) => {
    void queryClient.invalidateQueries({ queryKey: ['reports', 'event-summary', toValue(eventId)] })
    void queryClient.invalidateQueries({
      queryKey: ['reports', 'activity-assignments', vars.activityId],
    })
  }

  const assign = useMutation({
    mutationFn: (vars: { activityId: string; userId: string; body: AssignRequest }) =>
      patchApiActivitiesActivityIdUserIdAssign(vars.activityId, vars.userId, vars.body).then(
        (r) => r.data,
      ),
    onSuccess: invalidate,
  })

  const unassign = useMutation({
    mutationFn: (vars: { activityId: string; userId: string }) =>
      patchApiActivitiesActivityIdUserIdUnassign(vars.activityId, vars.userId),
    onSuccess: invalidate,
  })

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

  function verifyOverlaps(activityId: string, userId: string) {
    return verifyOverlapsRequest(activityId, userId)
  }

  return { assign, unassign, changeStatus, changeRole, verifyOverlaps }
}
