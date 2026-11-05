import type { QueryClient } from '@tanstack/vue-query'
import type { Router } from 'vue-router'

import { currentUser, hasStaffAccess, resolveSession, type AuthUser } from '@/entities/session'
import type { RouteAccess } from '@/shared/routes'

/** Admin pages `/admin` leads to, in the order of the admin sidebar. */
const ADMIN_HOMES = [
  { name: 'admin-dashboard' },
  { name: 'admin-events' },
  { name: 'admin-audit' },
  { name: 'admin-blocked-data' },
] as const

function allows(user: AuthUser, access: RouteAccess | undefined): boolean {
  if (access === undefined || access === 'user') return true
  return access !== 'guest' && hasStaffAccess(user, access)
}

/**
 * Lets a navigation through only when the target's `meta.access` allows it. Guest pages send a
 * known user home without asking the API; every other protected page resolves the session first,
 * sending guests to login with a `redirect` back and signed-in users without that access home.
 * `/admin` leads to the first admin page the user can open, or home when there is none.
 */
export function installAccessGuard(router: Router, queryClient: QueryClient): void {
  function adminHome(user: AuthUser) {
    const home = ADMIN_HOMES.find((page) => allows(user, router.resolve(page).meta.access))
    return home ?? ({ name: 'home' } as const)
  }

  router.beforeEach(async (to) => {
    const access = to.meta.access
    if (access === undefined) return true
    if (access === 'guest') return currentUser(queryClient) ? { name: 'home' } : true

    const user = await resolveSession(queryClient)
    if (!user) return { name: 'login', query: { redirect: to.fullPath } }
    if (to.name === 'admin') return adminHome(user)
    return allows(user, access) ? true : { name: 'home' }
  })
}
