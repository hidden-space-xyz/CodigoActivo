import { useQuery } from '@tanstack/vue-query'

import {
  getApiActivitiesAssignmentStatusTypes,
  getApiActivitiesModalityTypes,
  getApiActivitiesRoleType,
} from '@/shared/api/generated/endpoints/activities/activities'
import { getApiEventsCategoryType } from '@/shared/api/generated/endpoints/events/events'
import { getApiResourcesTypes } from '@/shared/api/generated/endpoints/resources/resources'
import {
  getApiUsersStatusTypes,
  getApiUsersTypes,
} from '@/shared/api/generated/endpoints/users/users'

import { catalogQueryKeys } from './query-keys'

export function useUserTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.userTypes,
    queryFn: () => getApiUsersTypes().then((r) => r.data ?? []),
  })
}

export function useActivityRoleTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.activityRoleTypes,
    queryFn: () => getApiActivitiesRoleType().then((r) => r.data ?? []),
  })
}

export function useAssignmentStatusTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.assignmentStatusTypes,
    queryFn: () => getApiActivitiesAssignmentStatusTypes().then((r) => r.data ?? []),
  })
}

export function useEventCategoryTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.eventCategoryTypes,
    queryFn: () => getApiEventsCategoryType({ pageSize: 100 }).then((r) => r.data.items ?? []),
  })
}

export function useUserStatusTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.userStatusTypes,
    queryFn: () => getApiUsersStatusTypes().then((r) => r.data ?? []),
  })
}

export function useActivityModalityTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.activityModalityTypes,
    queryFn: () => getApiActivitiesModalityTypes().then((r) => r.data ?? []),
  })
}

export function useResourceTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.resourceTypes,
    queryFn: () => getApiResourcesTypes().then((r) => r.data ?? []),
  })
}
