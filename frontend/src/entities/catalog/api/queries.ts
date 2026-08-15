import { useQuery } from '@tanstack/vue-query'

import { catalogQueryKeys } from './query-keys'
import {
  getActivityModalityTypesRequest,
  getActivityRoleTypesRequest,
  getAssignmentStatusTypesRequest,
  getEventCategoryTypesRequest,
  getResourceTypesRequest,
  getTermsDocumentsRequest,
  getUserStatusTypesRequest,
  getUserTypesRequest,
} from './requests'

export function useUserTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.userTypes(),
    queryFn: () => getUserTypesRequest(),
  })
}

export function useActivityRoleTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.activityRoleTypes(),
    queryFn: () => getActivityRoleTypesRequest(),
  })
}

export function useAssignmentStatusTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.assignmentStatusTypes(),
    queryFn: () => getAssignmentStatusTypesRequest(),
  })
}

export function useEventCategoryTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.eventCategoryTypes(),
    queryFn: () => getEventCategoryTypesRequest(),
  })
}

export function useUserStatusTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.userStatusTypes(),
    queryFn: () => getUserStatusTypesRequest(),
  })
}

export function useActivityModalityTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.activityModalityTypes(),
    queryFn: () => getActivityModalityTypesRequest(),
  })
}

export function useResourceTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.resourceTypes(),
    queryFn: () => getResourceTypesRequest(),
  })
}

export function useTermsDocumentsList() {
  return useQuery({
    queryKey: catalogQueryKeys.termsDocuments(),
    queryFn: () => getTermsDocumentsRequest(),
  })
}
