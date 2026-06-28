import type { RouteLocationNormalized, RouteLocationRaw } from 'vue-router'

import { getApiAuthMe } from '@/shared/api/generated/endpoints/auth/auth'
import { useSessionStore } from '@/modules/auth/presentation/stores/session.store'

export function redirectIfAuthenticated(): RouteLocationRaw | true {
  const session = useSessionStore()
  return session.isAuthenticated ? { name: 'home' } : true
}

async function ensureSession(): Promise<boolean> {
  const session = useSessionStore()
  if (session.isAuthenticated) return true
  try {
    const response = await getApiAuthMe()
    session.setUser(response.data)
    return true
  } catch {
    return false
  }
}

export async function requireAuth(to: RouteLocationNormalized): Promise<RouteLocationRaw | true> {
  if (await ensureSession()) return true
  return { name: 'login', query: { redirect: to.fullPath } }
}

export async function requireAdmin(to: RouteLocationNormalized): Promise<RouteLocationRaw | true> {
  if (!(await ensureSession())) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }
  return useSessionStore().isAdmin ? true : { name: 'home' }
}
