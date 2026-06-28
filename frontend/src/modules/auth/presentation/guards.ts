import type { RouteLocationNormalized, RouteLocationRaw } from 'vue-router'

import { getCurrentUser } from '@/modules/auth/application/use-cases/get-current-user.use-case'
import { authRepository } from '@/modules/auth/infrastructure/repositories/auth-repository.provider'
import { useSessionStore } from '@/modules/auth/presentation/stores/session.store'

export function redirectIfAuthenticated(): RouteLocationRaw | true {
  const session = useSessionStore()
  return session.isAuthenticated ? { name: 'home' } : true
}

async function ensureSession(): Promise<boolean> {
  const session = useSessionStore()
  if (session.isAuthenticated) return true
  const user = await getCurrentUser(authRepository)
  session.setUser(user)
  return user !== null
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
