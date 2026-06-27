import { useQuery } from '@tanstack/vue-query'
import {
  deleteApiActivitiesRoleTypeActivityRoleTypeId,
  getApiActivitiesAssignmentStatusTypes,
  getApiActivitiesRoleTypes,
  postApiActivitiesRoleType,
  putApiActivitiesRoleTypeActivityRoleTypeId,
} from '@/shared/api/generated/endpoints/activities/activities'
import { getApiUsersTypes } from '@/shared/api/generated/endpoints/users/users'
import { queryKeys } from '@/shared/api/query-keys'
import { useCatalog } from '@/features/catalogs/useCatalog'

export function useActivityRoleTypes() {
  return useCatalog({
    queryKey: queryKeys.activityRoleTypes,
    fetchAll: (signal) => getApiActivitiesRoleTypes({ signal }).then((r) => r.data ?? []),
    create: (body) => postApiActivitiesRoleType(body),
    update: (id, body) => putApiActivitiesRoleTypeActivityRoleTypeId(id, body),
    remove: (id) => deleteApiActivitiesRoleTypeActivityRoleTypeId(id),
  })
}

export function useAssignmentStatusTypesList() {
  return useQuery({
    queryKey: queryKeys.assignmentStatusTypes,
    queryFn: ({ signal }) =>
      getApiActivitiesAssignmentStatusTypes({ signal }).then((r) => r.data ?? []),
  })
}

export function useUserTypesList() {
  return useQuery({
    queryKey: queryKeys.userTypes,
    queryFn: ({ signal }) => getApiUsersTypes({ signal }).then((r) => r.data ?? []),
  })
}
