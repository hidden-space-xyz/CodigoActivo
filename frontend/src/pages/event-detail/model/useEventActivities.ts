import { computed } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

import {
  activityQueryKeys,
  assignActivityRequest,
  assignHouseholdRequest,
  getEventActivitiesRequest,
  getHouseholdAssignmentsRequest,
  getHouseholdMembersRequest,
  getMyAssignmentsRequest,
  getSignupRolesRequest,
  unassignActivityRequest,
  verifyOverlapsRequest,
} from '@/entities/activity'
import type {
  ActivityRole,
  HouseholdAssignmentInput,
  HouseholdMember,
  OverlapCheck,
} from '@/entities/activity'
import { useSession } from '@/entities/session'

export function useEventActivities(eventId: () => string) {
  const session = useSession()
  const queryClient = useQueryClient()

  const userId = computed(() => session.user?.id ?? null)
  const isAuthenticated = computed(() => userId.value !== null)

  const activitiesKey = computed(() => activityQueryKeys.publicByEvent(eventId()))
  const assignedKey = activityQueryKeys.myAssignments()
  const membersKey = activityQueryKeys.householdMembers()
  const householdKey = computed(() => activityQueryKeys.householdAssignments(eventId()))

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

  const signupRoles = useQuery({
    queryKey: computed(() => activityQueryKeys.signupRoles(userId.value ?? '')),
    queryFn: () => getSignupRolesRequest(),
    enabled: isAuthenticated,
  })

  const rolesByUserId = computed(() => {
    const map = new Map<string, readonly ActivityRole[]>()
    for (const member of signupRoles.data.value ?? []) {
      map.set(member.userId, member.roles)
    }
    return map
  })

  function rolesFor(memberId: string): readonly ActivityRole[] {
    return rolesByUserId.value.get(memberId) ?? []
  }

  const selfRoles = computed<readonly ActivityRole[]>(() =>
    userId.value ? rolesFor(userId.value) : [],
  )

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
    signupRoles,
    selfRoles,
    rolesFor,
    assign,
    assignHousehold,
    unassign,
    verifyOverlaps,
    isAuthenticated,
  }
}
