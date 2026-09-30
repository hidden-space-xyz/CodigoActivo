import { queryOptions } from '@tanstack/vue-query'

import { getCurrentUserRequest, getLoginChallengeRequest } from './requests'

/** Query keys of the session; `all` covers every one of them. */
const sessionKeys = {
  all: ['session'] as const,
  me: () => [...sessionKeys.all, 'me'] as const,
  loginChallenge: () => [...sessionKeys.all, 'login-challenge'] as const,
}

/** Query options of the session, shared by guards, views and tests. */
export const sessionQueries = {
  /**
   * The signed-in user, or `null` for a guest. A known user stays cached until a mutation
   * invalidates it or the session ends, and views never refetch it by themselves; a guest is asked
   * again on every guarded navigation, so signing in from another tab is noticed.
   */
  me: () =>
    queryOptions({
      queryKey: sessionKeys.me(),
      queryFn: () => getCurrentUserRequest(),
      staleTime: (query) => (query.state.data ? Infinity : 0),
      gcTime: Infinity,
      retry: false,
      refetchOnMount: false,
      refetchOnWindowFocus: false,
      refetchOnReconnect: false,
    }),
  /** Pending second step of a login, always read fresh because the cookie behind it expires. */
  loginChallenge: () =>
    queryOptions({
      queryKey: sessionKeys.loginChallenge(),
      queryFn: () => getLoginChallengeRequest(),
      staleTime: 0,
      gcTime: 0,
    }),
}
