import type { QueryClient } from '@tanstack/vue-query'

import { currentUser, hasStaffAccess } from '@/entities/session'

/** The session a view would see, read from `queryClient` outside any component. */
export function sessionOf(queryClient: QueryClient) {
  const user = currentUser(queryClient)
  return {
    user,
    isAuthenticated: user !== null,
    isEditor: hasStaffAccess(user, 'editor'),
    isAdmin: hasStaffAccess(user, 'admin'),
    isSuperAdmin: user?.isSuperAdmin ?? false,
    isPrivacyOfficer: user?.isPrivacyOfficer ?? false,
    displayName: user?.firstName ?? '',
  }
}
