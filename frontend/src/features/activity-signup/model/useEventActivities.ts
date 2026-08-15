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
import { eventQueryKeys, getEventTermsAcceptanceRequest } from '@/entities/event'
import { useSession } from '@/entities/session'
import { i18n } from '@/shared/i18n'

export function useEventActivities(eventId: () => string, hasTerms: () => boolean) {
  const session = useSession()
  const queryClient = useQueryClient()

  const userId = computed(() => session.user?.id ?? null)
  const isAuthenticated = computed(() => userId.value !== null)

  const activitiesKey = computed(() => activityQueryKeys.publicByEvent(eventId()))
  const assignedKey = computed(() => activityQueryKeys.myAssignments(eventId()))
  const membersKey = activityQueryKeys.householdMembers()
  const householdKey = computed(() => activityQueryKeys.householdAssignments(eventId()))
  const termsAcceptanceKey = computed(() => eventQueryKeys.termsAcceptance(eventId()))

  const activities = useQuery({
    queryKey: activitiesKey,
    queryFn: () => getEventActivitiesRequest(eventId()),
  })

  const assigned = useQuery({
    queryKey: assignedKey,
    queryFn: () => getMyAssignmentsRequest(eventId()),
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
    queryKey: activityQueryKeys.signupRoles(),
    queryFn: () => getSignupRolesRequest(),
    enabled: isAuthenticated,
  })

  const termsAccepted = useQuery({
    queryKey: termsAcceptanceKey,
    queryFn: () => getEventTermsAcceptanceRequest(eventId()),
    enabled: computed(() => isAuthenticated.value && hasTerms()),
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

  const membershipReady = computed(
    () =>
      !isAuthenticated.value || (!assigned.isLoading.value && !householdMembers.isLoading.value),
  )

  const members = computed<HouseholdMember[]>(() => {
    const self: HouseholdMember = {
      id: userId.value ?? '',
      name: session.user?.firstName ?? i18n.global.t('features.activitySignup.selfMember'),
    }
    return [self, ...(householdMembers.data.value ?? [])]
  })

  function invalidate(): void {
    void queryClient.invalidateQueries({ queryKey: activitiesKey.value })
    void queryClient.invalidateQueries({ queryKey: assignedKey.value })
    void queryClient.invalidateQueries({ queryKey: householdKey.value })
  }

  function invalidateAfterSignup(): void {
    invalidate()
    void queryClient.invalidateQueries({ queryKey: termsAcceptanceKey.value })
  }

  const assign = useMutation({
    mutationFn: (vars: {
      activityId: string
      activityRoleTypeId: string
      acceptTerms: boolean
    }) => {
      if (!userId.value)
        return Promise.reject(new Error(i18n.global.t('features.activitySignup.notAuthenticated')))
      return assignActivityRequest(
        vars.activityId,
        userId.value,
        vars.activityRoleTypeId,
        vars.acceptTerms,
      )
    },
    onSuccess: invalidateAfterSignup,
  })

  const assignHousehold = useMutation({
    mutationFn: (vars: {
      activityId: string
      assignments: HouseholdAssignmentInput[]
      acceptTerms: boolean
    }) => assignHouseholdRequest(vars.activityId, vars.assignments, vars.acceptTerms),
    onSuccess: invalidateAfterSignup,
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
    membershipReady,
    members,
    userId,
    signupRoles,
    selfRoles,
    rolesFor,
    assign,
    assignHousehold,
    unassign,
    verifyOverlaps,
    termsAccepted,
    isAuthenticated,
  }
}
