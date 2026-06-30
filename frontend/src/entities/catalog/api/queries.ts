import { useQuery } from '@tanstack/vue-query'

import {
  getApiActivitiesAssignmentStatusTypes,
  getApiActivitiesRoleTypes,
} from '@/shared/api/generated/endpoints/activities/activities'
import { getApiUsersTypes } from '@/shared/api/generated/endpoints/users/users'

import { catalogQueryKeys } from './query-keys'

export function useUserTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.userTypes,
    queryFn: ({ signal }) => getApiUsersTypes({ signal }).then((r) => r.data ?? []),
  })
}

export function useActivityRoleTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.activityRoleTypes,
    queryFn: ({ signal }) => getApiActivitiesRoleTypes({ signal }).then((r) => r.data ?? []),
  })
}

export function useAssignmentStatusTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.assignmentStatusTypes,
    queryFn: ({ signal }) =>
      getApiActivitiesAssignmentStatusTypes({ signal }).then((r) => r.data ?? []),
  })
}
