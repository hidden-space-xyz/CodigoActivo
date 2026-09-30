import { useQueryClient } from '@tanstack/vue-query'
import { useRouter } from 'vue-router'

import { endSession, logoutRequest } from '@/entities/session'

/**
 * Signs the user out and goes to the login page. The local session and every cached query end even
 * when the API call fails, so a lost connection never leaves the user's data on screen.
 */
export function useLogout() {
  const queryClient = useQueryClient()
  const router = useRouter()

  return async function logout(): Promise<void> {
    try {
      await logoutRequest()
    } finally {
      endSession(queryClient)
      await router.push({ name: 'login' })
    }
  }
}
