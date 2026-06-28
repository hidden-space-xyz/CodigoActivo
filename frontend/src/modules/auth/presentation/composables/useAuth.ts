import { storeToRefs } from 'pinia'
import { useRouter } from 'vue-router'

import { getCurrentUser } from '@/modules/auth/application/use-cases/get-current-user.use-case'
import { logout as logoutUseCase } from '@/modules/auth/application/use-cases/logout.use-case'
import { authRepository } from '@/modules/auth/infrastructure/repositories/auth-repository.provider'
import { useSessionStore } from '@/modules/auth/presentation/stores/session.store'

export function useAuth() {
  const session = useSessionStore()
  const { user, isAuthenticated, isAdmin, displayName, roleNames } = storeToRefs(session)
  const router = useRouter()

  async function bootstrap(): Promise<void> {
    session.setUser(await getCurrentUser(authRepository))
  }

  async function logout(): Promise<void> {
    try {
      await logoutUseCase(authRepository)
    } finally {
      session.clear()
      await router.push({ name: 'login' })
    }
  }

  return { user, isAuthenticated, isAdmin, displayName, roleNames, bootstrap, logout }
}
