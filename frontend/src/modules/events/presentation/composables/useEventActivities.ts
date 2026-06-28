import { computed } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

import { assignActivity } from '@/modules/events/application/use-cases/assign-activity.use-case'
import { assignHousehold as assignHouseholdUseCase } from '@/modules/events/application/use-cases/assign-household.use-case'
import { getEventActivities } from '@/modules/events/application/use-cases/get-event-activities.use-case'
import { getHouseholdAssignments } from '@/modules/events/application/use-cases/get-household-assignments.use-case'
import { getHouseholdMembers } from '@/modules/events/application/use-cases/get-household-members.use-case'
import { getMyAssignments } from '@/modules/events/application/use-cases/get-my-assignments.use-case'
import { unassignActivity } from '@/modules/events/application/use-cases/unassign-activity.use-case'
import { verifyOverlaps as verifyOverlapsUseCase } from '@/modules/events/application/use-cases/verify-overlaps.use-case'
import type { HouseholdMember, OverlapCheck } from '@/modules/events/domain/entities/activity.entity'
import type { HouseholdAssignmentInput } from '@/modules/events/domain/value-objects/household-assignment-input'
import { activityRepository } from '@/modules/events/infrastructure/repositories/activity-repository.provider'
import { useSessionStore } from '@/modules/auth/presentation/stores/session.store'

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
    queryFn: () => getEventActivities(activityRepository, eventId()),
  })

  const assigned = useQuery({
    queryKey: assignedKey,
    queryFn: () => getMyAssignments(activityRepository),
    enabled: isAuthenticated,
  })

  const householdMembers = useQuery({
    queryKey: membersKey,
    queryFn: () => {
      if (!userId.value) return Promise.resolve<readonly HouseholdMember[]>([])
      return getHouseholdMembers(activityRepository, userId.value)
    },
    enabled: isAuthenticated,
  })

  const household = useQuery({
    queryKey: householdKey,
    queryFn: () => getHouseholdAssignments(activityRepository, eventId()),
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
      return assignActivity(activityRepository, vars.activityId, userId.value, vars.activityRoleTypeId)
    },
    onSuccess: invalidate,
  })

  const assignHousehold = useMutation({
    mutationFn: (vars: { activityId: string; assignments: HouseholdAssignmentInput[] }) =>
      assignHouseholdUseCase(activityRepository, vars.activityId, vars.assignments),
    onSuccess: invalidate,
  })

  const unassign = useMutation({
    mutationFn: (vars: { activityId: string; userId: string }) =>
      unassignActivity(activityRepository, vars.activityId, vars.userId),
    onSuccess: invalidate,
  })

  function verifyOverlaps(activityId: string): Promise<OverlapCheck | undefined> {
    if (!userId.value) return Promise.resolve(undefined)
    return verifyOverlapsUseCase(activityRepository, activityId, userId.value)
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
