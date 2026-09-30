import { computed } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import {
  activityMutations,
  activityQueries,
  type ActivityRole,
  type HouseholdMember,
  type OverlapCheck,
} from '@/entities/activity'
import { eventKeys, eventQueries } from '@/entities/event'
import { useSession } from '@/entities/session'
import { alsoInvalidates } from '@/shared/api'

/**
 * Signup state for an event's activities: the user's and household signups, household members
 * (the user listed first), allowed signup roles and terms state. Queries about the user stay
 * disabled for guests. Signups refresh the activities, the signups and the terms state.
 *
 * @param eventId - Getter so the queries follow route changes.
 * @param hasTerms - Getter; the terms state is only fetched when the event has terms documents.
 */
export function useEventActivities(eventId: () => string, hasTerms: () => boolean) {
  const { t } = useI18n()
  const session = useSession()
  const queryClient = useQueryClient()

  const userId = computed(() => session.user?.id ?? null)
  const isAuthenticated = computed(() => userId.value !== null)

  const activities = useQuery(() => activityQueries.ofEvent(eventId()))
  const assigned = useQuery(() => ({
    ...activityQueries.myAssignments(eventId()),
    enabled: isAuthenticated.value,
  }))
  const householdMembers = useQuery(() => ({
    ...activityQueries.householdMembers(userId.value ?? ''),
    enabled: isAuthenticated.value,
  }))
  const household = useQuery(() => ({
    ...activityQueries.householdAssignments(eventId()),
    enabled: isAuthenticated.value,
  }))
  const signupRoles = useQuery(() => ({
    ...activityQueries.signupRoles(),
    enabled: isAuthenticated.value,
  }))
  const termsState = useQuery(() => ({
    ...eventQueries.terms(eventId()),
    enabled: isAuthenticated.value && hasTerms(),
  }))

  const rolesByUserId = computed(() => {
    const map = new Map<string, readonly ActivityRole[]>()
    for (const member of signupRoles.data.value ?? []) map.set(member.userId, member.roles)
    return map
  })

  function rolesFor(memberId: string): readonly ActivityRole[] {
    return rolesByUserId.value.get(memberId) ?? []
  }

  const selfRoles = computed(() => (userId.value ? rolesFor(userId.value) : []))
  const hasHousehold = computed(() => (householdMembers.data.value ?? []).length > 0)
  const membershipReady = computed(
    () =>
      !isAuthenticated.value || (!assigned.isLoading.value && !householdMembers.isLoading.value),
  )
  const members = computed<HouseholdMember[]>(() => [
    { id: userId.value ?? '', name: session.user?.firstName ?? t('pages.eventDetail.selfMember') },
    ...(householdMembers.data.value ?? []),
  ])

  const assign = useMutation(() =>
    alsoInvalidates(
      activityMutations.signUp(eventId(), userId.value ?? ''),
      eventKeys.terms(eventId()),
    ),
  )
  const assignHousehold = useMutation(() =>
    alsoInvalidates(activityMutations.signUpHousehold(eventId()), eventKeys.terms(eventId())),
  )
  const unassign = useMutation(() => activityMutations.withdraw(eventId()))

  function verifyOverlaps(activityId: string): Promise<OverlapCheck | undefined> {
    if (!userId.value) return Promise.resolve(undefined)
    return queryClient.fetchQuery(activityQueries.overlaps(activityId, userId.value))
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
    termsState,
    isAuthenticated,
  }
}
