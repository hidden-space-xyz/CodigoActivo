import type { QueryClient } from '@tanstack/vue-query'

import { sessionQueries } from '../api/queries'
import type { AuthUser } from './types'

/** The signed-in user the cache already knows, without asking the API; `null` for a guest. */
export function currentUser(queryClient: QueryClient): AuthUser | null {
  return queryClient.getQueryData(sessionQueries.me().queryKey) ?? null
}

/** The signed-in user, asking the API unless one is already known; `null` for a guest. */
export function resolveSession(queryClient: QueryClient): Promise<AuthUser | null> {
  return queryClient.fetchQuery(sessionQueries.me())
}

/** Reloads the signed-in user after a change the session shows, such as a new name. */
export function refreshSession(queryClient: QueryClient): Promise<AuthUser | null> {
  return queryClient.fetchQuery({ ...sessionQueries.me(), staleTime: 0 })
}

/** Stores the user the API has just signed in as the session. */
export function startSession(queryClient: QueryClient, user: AuthUser): void {
  queryClient.setQueryData(sessionQueries.me().queryKey, user)
}

/**
 * Leaves a guest session behind: forgets every other cached query and mutation, which belonged to
 * the user, and only then clears the user so no view briefly shows their data as a guest's.
 */
export function endSession(queryClient: QueryClient): void {
  const me = sessionQueries.me().queryKey
  queryClient.getMutationCache().clear()
  queryClient.removeQueries({ predicate: (query) => query.queryKey[0] !== me[0] })
  queryClient.setQueryData(me, null)
}
