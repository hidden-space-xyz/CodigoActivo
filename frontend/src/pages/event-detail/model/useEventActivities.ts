import { computed } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

import {
  assignActivityRequest,
  assignHouseholdRequest,
  getEventActivitiesRequest,
  getHouseholdAssignmentsRequest,
  getHouseholdMembersRequest,
  getMyAssignmentsRequest,
  unassignActivityRequest,
  verifyOverlapsRequest,
} from '@/entities/activity'
import type { HouseholdAssignmentInput, HouseholdMember, OverlapCheck } from '@/entities/activity'
import { useSessionStore } from '@/entities/session'

export function useEventActivities(eventId: () => string) {
  const session = useSessionStore()
  const queryClient = useQueryClient()

  const userId = computed(() => session.user?.id ?? null)
  const isAuthenticated = computed(() => userId.value !== null)

  const activitiesKey = computed(() => ['public', 'event-activities', eventId()] as const)
  const assignedKey = ['public', 'my-assignments'] as const
  const membersKey = ['public', 'my-children'] as const
  const householdKey = computed(() => ['public', 'household-assignments', eventId()] as const)

  const activities = useQuery({
    queryKey: activitiesKey,
    queryFn: () => getEventActivitiesRequest(eventId()),
  })

  const assigned = useQuery({
    queryKey: assignedKey,
    queryFn: () => getMyAssignmentsRequest(),
    enabled: isAuthenticated,
  })

  const householdMembers = useQuery({
    queryKey: membersKey,
    queryFn: () => {
      if (!userId.value) return Promise.resolve<readonly HouseholdMember[]>([])
      return getHouseholdMembersRequest(userId.value)
    },
    enabled: isAuthenticated,
  })

  const household = useQuery({
    queryKey: householdKey,
    queryFn: () => getHouseholdAssignmentsRequest(eventId()),
    enabled: isAuthenticated,
  })

  const hasHousehold = computed(() => (householdMembers.data.value ?? []).length > 0)

  const members = computed<HouseholdMember[]>(() => {
    const self: HouseholdMember = { id: userId.value ?? '', name: session.user?.firstName ?? 'Yo' }
    return [self, ...(householdMembers.data.value ?? [])]
  })

  function invalidate(): void {
    void queryClient.invalidateQueries({ queryKey: activitiesKey.value })
    void queryClient.invalidateQueries({ queryKey: assignedKey })
    void queryClient.invalidateQueries({ queryKey: householdKey.value })
  }

  const assign = useMutation({
    mutationFn: (vars: { activityId: string; activityRoleTypeId: string }) => {
      if (!userId.value) return Promise.reject(new Error('No autenticado'))
      return assignActivityRequest(vars.activityId, userId.value, vars.activityRoleTypeId)
    },
    onSuccess: invalidate,
  })

  const assignHousehold = useMutation({
    mutationFn: (vars: { activityId: string; assignments: HouseholdAssignmentInput[] }) =>
      assignHouseholdRequest(vars.activityId, vars.assignments),
    onSuccess: invalidate,
  })

  const unassign = useMutation({
    mutationFn: (vars: { activityId: string; userId: string }) =>
      unassignActivityRequest(vars.activityId, vars.userId),
    onSuccess: invalidate,
  })

  function verifyOverlaps(activityId: string): Promise<OverlapCheck | undefined> {
    if (!userId.value) return Promise.resolve(undefined)
    return verifyOverlapsRequest(activityId, userId.value)
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
