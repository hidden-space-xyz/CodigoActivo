import { storeToRefs } from 'pinia'
import { useRouter } from 'vue-router'

import { getCurrentUserRequest, logoutRequest, useSessionStore } from '@/entities/session'

export function useAuth() {
  const session = useSessionStore()
  const { user, isAuthenticated, isAdmin, displayName, roleNames } = storeToRefs(session)
  const router = useRouter()

  async function bootstrap(): Promise<void> {
    session.setUser(await getCurrentUserRequest())
  }

  async function logout(): Promise<void> {
    try {
      await logoutRequest()
    } finally {
      session.clear()
      await router.push({ name: 'login' })
    }
  }

  return { user, isAuthenticated, isAdmin, displayName, roleNames, bootstrap, logout }
}
