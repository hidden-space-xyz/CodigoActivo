import { storeToRefs } from 'pinia'
import { useRouter } from 'vue-router'

import { getApiAuthMe, postApiAuthLogout } from '@/shared/api/generated/endpoints/auth/auth'
import { resetCsrfToken } from '@/shared/api/http-client'
import { useSessionStore } from '@/modules/auth/presentation/stores/session.store'

export function useAuth() {
  const session = useSessionStore()
  const { user, isAuthenticated, isAdmin, displayName, roleNames } = storeToRefs(session)
  const router = useRouter()

  async function bootstrap(): Promise<void> {
    try {
      const response = await getApiAuthMe()
      session.setUser(response.data)
    } catch {
      session.clear()
    }
  }

  async function logout(): Promise<void> {
    try {
      await postApiAuthLogout()
    } catch {}
    session.clear()
    resetCsrfToken()
    await router.push({ name: 'login' })
  }

  return { user, isAuthenticated, isAdmin, displayName, roleNames, bootstrap, logout }
}
