import type { RouteLocationNormalized, RouteLocationRaw } from 'vue-router'

import { useSession } from '@/entities/session'

/**
 * Route guard for guest-only pages (login, registration): sends signed-in users home. Reads the
 * current session without resolving it.
 */
export function redirectIfAuthenticated(): RouteLocationRaw | true {
  const session = useSession()
  return session.isAuthenticated ? { name: 'home' } : true
}

async function ensureSession(): Promise<boolean> {
  return (await useSession().resolve()) !== null
}

/** Route guard that resolves the session and sends guests to login with a `redirect` back here. */
export async function requireAuth(to: RouteLocationNormalized): Promise<RouteLocationRaw | true> {
  if (await ensureSession()) return true
  return { name: 'login', query: { redirect: to.fullPath } }
}

/** Like `requireAuth`, but also sends signed-in non-admin users home. */
export async function requireAdmin(to: RouteLocationNormalized): Promise<RouteLocationRaw | true> {
  if (!(await ensureSession())) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }
  return useSession().isAdmin ? true : { name: 'home' }
}
