import { useQuery } from '@tanstack/vue-query'

import type {
  ActivityRoleTypeResponse,
  EventCategoryTypeResponse,
} from '@/shared/api/generated/models'
import type {
  ActivityModalityTypeResponse,
  AssignmentStatusTypeResponse,
  UserTypeResponse,
} from '@/shared/api'
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

export function useEventCategoryTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.eventCategoryTypes,
    queryFn: () =>
      fetchODataList<EventCategoryTypeResponse>('EventCategoryTypes', {
        orderBy: 'name asc',
        top: 1000,
      }).then((r) => r.items),
  })
}

export function useActivityModalityTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.activityModalityTypes,
    queryFn: () =>
      fetchODataList<ActivityModalityTypeResponse>('ActivityModalityTypes', {
        orderBy: 'name asc',
        top: 1000,
      }).then((r) => r.items),
  })
}
