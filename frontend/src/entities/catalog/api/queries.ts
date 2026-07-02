import { useQuery } from '@tanstack/vue-query'

import type { ActivityRoleTypeResponse } from '@/shared/api/generated/models'
import type { AssignmentStatusTypeResponse, UserTypeResponse } from '@/shared/api'
import { fetchODataList } from '@/shared/api'

import { catalogQueryKeys } from './query-keys'

export function useUserTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.userTypes,
    queryFn: () =>
      fetchODataList<UserTypeResponse>('UserTypes', { orderBy: 'name asc', top: 1000 }).then(
        (r) => r.items,
      ),
  })
}

export function useActivityRoleTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.activityRoleTypes,
    queryFn: () =>
      fetchODataList<ActivityRoleTypeResponse>('ActivityRoleTypes', {
        orderBy: 'name asc',
        top: 1000,
      }).then((r) => r.items),
  })
}

export function useAssignmentStatusTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.assignmentStatusTypes,
    queryFn: () =>
      fetchODataList<AssignmentStatusTypeResponse>('AssignmentStatusTypes', {
        orderBy: 'name asc',
        top: 1000,
      }).then((r) => r.items),
  })
}
