import { queryOptions } from '@tanstack/vue-query'

import type { ServerTableSource } from '@/shared/lib/paging'

import type { ActivityListing, ActivityListParams } from '../model/types'
import {
  getActivitiesPageRequest,
  getActivityModalitiesRequest,
  getActivityRequest,
  getActivityRolesRequest,
  getAssignmentStatusesRequest,
  getEventActivitiesRequest,
  getHouseholdAssignmentsRequest,
  getHouseholdMembersRequest,
  getMyAssignmentsRequest,
  getSignupRolesRequest,
  verifyOverlapsRequest,
} from './requests'

/** Query keys of activities and their signups; `all` covers every one of them. */
export const activityKeys = {
  all: ['activities'] as const,
  ofEvent: (eventId: string) => [...activityKeys.all, 'of-event', eventId] as const,
  myAssignments: (eventId: string) => [...activityKeys.all, 'my-assignments', eventId] as const,
  householdAssignments: (eventId: string) =>
    [...activityKeys.all, 'household-assignments', eventId] as const,
  householdMembers: () => [...activityKeys.all, 'household-members'] as const,
  signupRoles: () => [...activityKeys.all, 'signup-roles'] as const,
  overlaps: (activityId: string, userId: string) =>
    [...activityKeys.all, 'overlaps', activityId, userId] as const,
  detail: (id: string) => [...activityKeys.all, 'detail', id] as const,
  list: () => [...activityKeys.all, 'list'] as const,
}

/**
 * Query keys of the activity catalogs (roles, signup statuses and modalities), kept apart from
 * `activityKeys.all` because no activity change touches them.
 */
export const activityCatalogKeys = {
  all: ['activity-catalogs'] as const,
  roles: () => [...activityCatalogKeys.all, 'roles'] as const,
  statuses: () => [...activityCatalogKeys.all, 'statuses'] as const,
  modalities: () => [...activityCatalogKeys.all, 'modalities'] as const,
}

/** Query options of activities, their signups and their catalogs. */
export const activityQueries = {
  /** Activities of the event ordered by start time. */
  ofEvent: (eventId: string) =>
    queryOptions({
      queryKey: activityKeys.ofEvent(eventId),
      queryFn: () => getEventActivitiesRequest(eventId),
    }),
  /** The signed-in user's own signups to the event's activities. */
  myAssignments: (eventId: string) =>
    queryOptions({
      queryKey: activityKeys.myAssignments(eventId),
      queryFn: () => getMyAssignmentsRequest(eventId),
    }),
  /** Signups of the signed-in user's household to the event's activities. */
  householdAssignments: (eventId: string) =>
    queryOptions({
      queryKey: activityKeys.householdAssignments(eventId),
      queryFn: () => getHouseholdAssignmentsRequest(eventId),
    }),
  /** Minors under `userId`, whom they can sign up as well. */
  householdMembers: (userId: string) =>
    queryOptions({
      queryKey: [...activityKeys.householdMembers(), userId] as const,
      queryFn: () => getHouseholdMembersRequest(userId),
    }),
  /** Roles each household member may sign up for. */
  signupRoles: () =>
    queryOptions({
      queryKey: activityKeys.signupRoles(),
      queryFn: () => getSignupRolesRequest(),
    }),
  /** Activities of `userId` whose schedule collides with this one; always asked afresh. */
  overlaps: (activityId: string, userId: string) =>
    queryOptions({
      queryKey: activityKeys.overlaps(activityId, userId),
      queryFn: () => verifyOverlapsRequest(activityId, userId),
      staleTime: 0,
      gcTime: 0,
    }),
  /** An activity as the admin edit form fills in, or `null` when it no longer exists. */
  detail: (id: string) =>
    queryOptions({
      queryKey: activityKeys.detail(id),
      queryFn: () => getActivityRequest(id),
    }),
  /** Roles a participant can take in an activity. */
  roles: () =>
    queryOptions({
      queryKey: activityCatalogKeys.roles(),
      queryFn: () => getActivityRolesRequest(),
    }),
  /** Statuses an activity signup can move through. */
  statuses: () =>
    queryOptions({
      queryKey: activityCatalogKeys.statuses(),
      queryFn: () => getAssignmentStatusesRequest(),
    }),
  /** Modalities an activity can be offered in. */
  modalities: () =>
    queryOptions({
      queryKey: activityCatalogKeys.modalities(),
      queryFn: () => getActivityModalitiesRequest(),
    }),
}

/** Admin activities table, paged, filtered and sorted by the API; `eventId` scopes it. */
export const activityList: ServerTableSource<ActivityListing, ActivityListParams> = {
  queryKey: activityKeys.list(),
  fetchPage: (params) => getActivitiesPageRequest(params),
}
