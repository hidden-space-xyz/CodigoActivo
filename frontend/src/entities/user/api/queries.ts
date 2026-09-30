import { queryOptions } from '@tanstack/vue-query'

import type { ServerTableSource } from '@/shared/lib/paging'

import type { User, UserListParams } from '../model/types'
import {
  getUserListPageRequest,
  getUserRequest,
  getUserStatusesRequest,
  getUserTypesRequest,
} from './requests'

/**
 * Query keys of users; `all` covers the users themselves. Their types and statuses are catalogs
 * no change to a user alters, so they live apart.
 */
export const userKeys = {
  all: ['users'] as const,
  list: () => [...userKeys.all, 'list'] as const,
  detail: (id: string) => [...userKeys.all, 'detail', id] as const,
  types: () => ['user-types'] as const,
  statuses: () => ['user-statuses'] as const,
}

/** Query options of users. */
export const userQueries = {
  /** A user by id. */
  detail: (id: string) =>
    queryOptions({
      queryKey: userKeys.detail(id),
      queryFn: () => getUserRequest(id),
    }),
  /** Every user type, for selectors and filters. */
  types: () =>
    queryOptions({
      queryKey: userKeys.types(),
      queryFn: () => getUserTypesRequest(),
    }),
  /** Every account status, for filters. */
  statuses: () =>
    queryOptions({
      queryKey: userKeys.statuses(),
      queryFn: () => getUserStatusesRequest(),
    }),
}

/** Admin user list, paged, filtered and sorted by the API. */
export const userList: ServerTableSource<User, UserListParams> = {
  queryKey: userKeys.list(),
  fetchPage: (params) => getUserListPageRequest(params),
}
