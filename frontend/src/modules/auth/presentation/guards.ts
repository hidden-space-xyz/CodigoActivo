import type { RouteLocationNormalized, RouteLocationRaw } from 'vue-router'

import { getApiAuthMe } from '@/shared/api/generated/endpoints/auth/auth'
import { useSessionStore } from '@/modules/auth/presentation/stores/session.store'

export function redirectIfAuthenticated(): RouteLocationRaw | true {
  const session = useSessionStore()
  return session.isAuthenticated ? { name: 'home' } : true
}

export async function requireAuth(to: RouteLocationNormalized): Promise<RouteLocationRaw | true> {
  const session = useSessionStore()
  if (session.isAuthenticated) return true
  try {
    const response = await getApiAuthMe()
    session.setUser(response.data)
    return true
  } catch {
    return { name: 'login', query: { redirect: to.fullPath } }
  }
}
