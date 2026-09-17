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

/** Cached user types, used to filter and label users in admin screens. */
export function useUserTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.userTypes(),
    queryFn: () => getUserTypesRequest(),
  })
}

/** Roles a participant can take in an activity, used for role capacities and attendee admin. */
export function useActivityRoleTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.activityRoleTypes(),
    queryFn: () => getActivityRoleTypesRequest(),
  })
}

/** Statuses an activity signup can move through, for the admin attendees tab. */
export function useAssignmentStatusTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.assignmentStatusTypes(),
    queryFn: () => getAssignmentStatusTypesRequest(),
  })
}

/** Event categories for selectors and filters (first 100); refreshed when a category is created. */
export function useEventCategoryTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.eventCategoryTypes(),
    queryFn: () => getEventCategoryTypesRequest(),
  })
}

/** Account statuses used by the admin users page filters. */
export function useUserStatusTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.userStatusTypes(),
    queryFn: () => getUserStatusTypesRequest(),
  })
}

/** Activity modalities offered when creating or filtering activities. */
export function useActivityModalityTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.activityModalityTypes(),
    queryFn: () => getActivityModalityTypesRequest(),
  })
}

/** Resource types for the admin resources page and resource form. */
export function useResourceTypesList() {
  return useQuery({
    queryKey: catalogQueryKeys.resourceTypes(),
    queryFn: () => getResourceTypesRequest(),
  })
}

/** Terms documents an event can require participants to accept (first 100). */
export function useTermsDocumentsList() {
  return useQuery({
    queryKey: catalogQueryKeys.termsDocuments(),
    queryFn: () => getTermsDocumentsRequest(),
  })
}
