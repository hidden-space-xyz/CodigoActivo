import { computed } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

import {
  getApiActivitiesActivityIdUserIdVerifyTimeOverlaps,
  getApiActivitiesAssigned,
  getApiActivitiesEventId,
  getApiActivitiesEventIdHouseholdAssignments,
  patchApiActivitiesActivityIdUserIdAssign,
  patchApiActivitiesActivityIdUserIdUnassign,
  postApiActivitiesActivityIdAssignHousehold,
} from '@/shared/api/generated/endpoints/activities/activities'
import { getApiUsersUserIdChildren } from '@/shared/api/generated/endpoints/users/users'
import type { HouseholdAssignmentRequest, TimeOverlapResponse } from '@/shared/api/generated/models'
import { useSessionStore } from '@/modules/auth/presentation/stores/session.store'

export interface HouseholdMember {
  id: string
  name: string
}

export function useEventActivities(eventId: () => string) {
  const session = useSessionStore()
  const queryClient = useQueryClient()

  const userId = computed(() => session.user?.id ?? null)
  const isAuthenticated = computed(() => userId.value !== null)

  const activitiesKey = computed(() => ['public', 'event-activities', eventId()] as const)
  const assignedKey = ['public', 'my-assignments'] as const
  const childrenKey = ['public', 'my-children'] as const
  const householdKey = computed(() => ['public', 'household-assignments', eventId()] as const)

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

  const children = useQuery({
    queryKey: childrenKey,
    queryFn: ({ signal }) => {
      if (!userId.value) return Promise.resolve([])
      return getApiUsersUserIdChildren(userId.value, { signal }).then((r) => r.data ?? [])
    },
    enabled: isAuthenticated,
  })

  const household = useQuery({
    queryKey: householdKey,
    queryFn: ({ signal }) =>
      getApiActivitiesEventIdHouseholdAssignments(eventId(), { signal }).then((r) => r.data ?? []),
    enabled: isAuthenticated,
  })

  const hasHousehold = computed(() => (children.data.value ?? []).length > 0)

  const members = computed<HouseholdMember[]>(() => {
    const self: HouseholdMember = { id: userId.value ?? '', name: session.user?.firstName ?? 'Yo' }
    const kids = (children.data.value ?? []).map((child) => ({
      id: child.id ?? '',
      name: `${child.firstName ?? ''} ${child.lastName ?? ''}`.trim(),
    }))
    return [self, ...kids]
  })

  function invalidate(): void {
    void queryClient.invalidateQueries({ queryKey: activitiesKey.value })
    void queryClient.invalidateQueries({ queryKey: assignedKey })
    void queryClient.invalidateQueries({ queryKey: householdKey.value })
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

  const assignHousehold = useMutation({
    mutationFn: (vars: { activityId: string; assignments: HouseholdAssignmentRequest[] }) =>
      postApiActivitiesActivityIdAssignHousehold(vars.activityId, {
        assignments: vars.assignments,
      }).then((r) => r.data),
    onSuccess: invalidate,
  })

  const unassign = useMutation({
    mutationFn: (vars: { activityId: string; userId: string }) =>
      patchApiActivitiesActivityIdUserIdUnassign(vars.activityId, vars.userId),
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
    household,
    hasHousehold,
    members,
    userId,
    assign,
    assignHousehold,
    unassign,
    verifyOverlaps,
    isAuthenticated,
  }
}
