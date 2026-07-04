import type { RouteLocationNormalized, RouteLocationRaw } from 'vue-router'

import { useSession } from '@/entities/session'

export function redirectIfAuthenticated(): RouteLocationRaw | true {
  const session = useSession()
  return session.isAuthenticated ? { name: 'home' } : true
}

async function ensureSession(): Promise<boolean> {
  return (await useSession().resolve()) !== null
}

export async function requireAuth(to: RouteLocationNormalized): Promise<RouteLocationRaw | true> {
  if (await ensureSession()) return true
  return { name: 'login', query: { redirect: to.fullPath } }
}

export async function requireAdmin(to: RouteLocationNormalized): Promise<RouteLocationRaw | true> {
  if (!(await ensureSession())) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }
  return useSession().isAdmin ? true : { name: 'home' }
}
