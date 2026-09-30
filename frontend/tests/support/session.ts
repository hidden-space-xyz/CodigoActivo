import type { QueryClient } from '@tanstack/vue-query'

import { currentUser } from '@/entities/session'

/** The session a view would see, read from `queryClient` outside any component. */
export function sessionOf(queryClient: QueryClient) {
  const user = currentUser(queryClient)
  return {
    user,
    isAuthenticated: user !== null,
    isAdmin: user?.isAdmin ?? false,
    displayName: user?.firstName ?? '',
  }
}
