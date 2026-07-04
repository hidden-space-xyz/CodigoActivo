import { toRefs } from 'vue'
import { useRouter } from 'vue-router'

import { logoutRequest, useSession } from '@/entities/session'

export function useAuth() {
  const session = useSession()
  const { isAuthenticated, isAdmin, displayName } = toRefs(session)
  const router = useRouter()

  async function bootstrap(): Promise<void> {
    await session.resolve()
  }

  async function logout(): Promise<void> {
    try {
      await logoutRequest()
    } finally {
      session.clear()
      await router.push({ name: 'login' })
    }
  }

  return { isAuthenticated, isAdmin, displayName, bootstrap, logout }
}
