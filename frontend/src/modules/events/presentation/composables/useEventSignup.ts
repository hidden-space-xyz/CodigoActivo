import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'

import { useSessionStore } from '@/modules/auth/presentation/stores/session.store'

export function useEventSignup(eventId: () => string) {
  const session = useSessionStore()
  const router = useRouter()

  const signedUp = ref(false)

  function signUp(): void {
    if (!session.isAuthenticated) {
      void router.push({ name: 'register', query: { redirect: `/events/${eventId()}` } })
      return
    }
    signedUp.value = true
  }

  return {
    signedUp,
    signUp,
    isAuthenticated: computed(() => session.isAuthenticated),
  }
}
