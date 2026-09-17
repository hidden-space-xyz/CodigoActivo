import { reactive } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useMutation } from '@tanstack/vue-query'

import { createEmptyCredentials, loginRequest, useSession } from '@/entities/session'
import type { Credentials } from '@/entities/session'

/**
 * Login form: on success stores the user in the session and navigates to the `redirect` query
 * parameter, or home when there is none.
 */
export function useLogin() {
  const session = useSession()
  const router = useRouter()
  const route = useRoute()

  const form = reactive<Credentials>(createEmptyCredentials())

  const mutation = useMutation({
    mutationFn: (credentials: Credentials) => loginRequest(credentials),
    onSuccess: (user) => {
      session.setUser(user)
      const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : null
      void router.push(redirect ?? { name: 'home' })
    },
  })

  function submit(): void {
    mutation.mutate({ ...form })
  }

  return {
    form,
    submit,
    isSubmitting: mutation.isPending,
    isError: mutation.isError,
  }
}
