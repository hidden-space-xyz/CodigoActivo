import { computed } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

import {
  getApiActivitiesActivityIdUserIdVerifyTimeOverlaps,
  getApiActivitiesAssigned,
  getApiActivitiesEventId,
  patchApiActivitiesActivityIdUserIdAssign,
  patchApiActivitiesActivityIdUserIdUnassign,
} from '@/shared/api/generated/endpoints/activities/activities'
import type { TimeOverlapResponse } from '@/shared/api/generated/models'
import { useSessionStore } from '@/modules/auth/presentation/stores/session.store'

/**
 * Public, volunteer-facing view of an event's activities: the activity list,
 * the current user's own assignments (status + role), and the actions to sign
 * up / drop out, including the time-overlap check.
 */
export function useEventActivities(eventId: () => string) {
  const session = useSessionStore()
  const queryClient = useQueryClient()

  const userId = computed(() => session.user?.id ?? null)
  const isAuthenticated = computed(() => userId.value !== null)

  const activitiesKey = computed(() => ['public', 'event-activities', eventId()] as const)
  const assignedKey = ['public', 'my-assignments'] as const

  const activities = useQuery({
    queryKey: activitiesKey,
    queryFn: ({ signal }) =>
      getApiActivitiesEventId(eventId(), { signal }).then((r) => r.data ?? []),
  })

  const assigned = useQuery({
    queryKey: assignedKey,
    queryFn: ({ signal }) => getApiActivitiesAssigned({}, { signal }).then((r) => r.data ?? []),
    enabled: isAuthenticated,
  })

  function invalidate(): void {
    void queryClient.invalidateQueries({ queryKey: activitiesKey.value })
    void queryClient.invalidateQueries({ queryKey: assignedKey })
  }

  const assign = useMutation({
    mutationFn: (vars: { activityId: string; activityRoleTypeId: string }) => {
      if (!userId.value) return Promise.reject(new Error('No autenticado'))
      return patchApiActivitiesActivityIdUserIdAssign(vars.activityId, userId.value, {
        activityRoleTypeId: vars.activityRoleTypeId,
      }).then((r) => r.data)
    },
    onSuccess: invalidate,
  })

  const unassign = useMutation({
    mutationFn: (activityId: string) => {
      if (!userId.value) return Promise.reject(new Error('No autenticado'))
      return patchApiActivitiesActivityIdUserIdUnassign(activityId, userId.value)
    },
    onSuccess: invalidate,
  })

  function verifyOverlaps(activityId: string): Promise<TimeOverlapResponse | undefined> {
    if (!userId.value) return Promise.resolve(undefined)
    return getApiActivitiesActivityIdUserIdVerifyTimeOverlaps(activityId, userId.value).then(
      (r) => r.data,
    )
  }

  return {
    activities,
    assigned,
    assign,
    unassign,
    verifyOverlaps,
    isAuthenticated,
  }
}
