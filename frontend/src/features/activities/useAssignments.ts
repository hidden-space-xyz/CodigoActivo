import { useMutation, useQueryClient } from '@tanstack/vue-query'

import {
  getApiActivitiesActivityIdUserIdVerifyTimeOverlaps,
  patchApiActivitiesActivityIdUserIdAssign,
  patchApiActivitiesActivityIdUserIdChangeStatus,
  patchApiActivitiesActivityIdUserIdUnassign,
} from '@/shared/api/generated/endpoints/activities/activities'
import type { AssignRequest, ChangeAssignmentStatusRequest } from '@/shared/api/generated/models'
import { queryKeys } from '@/shared/api/query-keys'

export function useAssignments(eventId: string) {
  const queryClient = useQueryClient()
  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: queryKeys.eventAssignments(eventId) })

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

  function verifyOverlaps(activityId: string, userId: string) {
    return getApiActivitiesActivityIdUserIdVerifyTimeOverlaps(activityId, userId).then(
      (r) => r.data,
    )
  }

  return { assign, unassign, changeStatus, verifyOverlaps }
}
