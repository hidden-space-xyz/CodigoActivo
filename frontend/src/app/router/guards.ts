import type { QueryClient } from '@tanstack/vue-query'
import type { Router } from 'vue-router'

import { currentUser, resolveSession } from '@/entities/session'

/**
 * Lets a navigation through only when the target's `meta.access` allows it. Guest pages send a
 * known user home without asking the API; user and admin pages resolve the session first, sending
 * guests to login with a `redirect` back and signed-in non-administrators home.
 */
export function installAccessGuard(router: Router, queryClient: QueryClient): void {
  router.beforeEach(async (to) => {
    const access = to.meta.access
    if (access === undefined) return true
    if (access === 'guest') return currentUser(queryClient) ? { name: 'home' } : true

    const user = await resolveSession(queryClient)
    if (!user) return { name: 'login', query: { redirect: to.fullPath } }
    return access === 'admin' && !user.isAdmin ? { name: 'home' } : true
  })
}
